using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MyDocuMgm.Application.ExternalFetch;

namespace MyDocuMgm.Infrastructure.ExternalFetch;

public sealed class ExternalHttpPageFetcher(
    HttpClient httpClient,
    SsrfSafeDestinationValidator destinationValidator,
    RobotsPolicyEvaluator robotsPolicy,
    IOptions<ExternalFetchOptions> configuredOptions) : IExternalPageFetcher
{
    public const string HttpClientName = "MyDocuMgm.ExternalFetch";
    private static readonly AsyncLocal<PinnedDestination?> CurrentPinnedDestination = new();
    private static readonly HashSet<HttpStatusCode> RedirectStatuses =
    [
        HttpStatusCode.Moved,
        HttpStatusCode.Redirect,
        HttpStatusCode.RedirectMethod,
        HttpStatusCode.TemporaryRedirect,
        HttpStatusCode.PermanentRedirect
    ];

    public async Task<ExternalPageResponse> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        var options = Effective(configuredOptions.Value);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));
        var token = timeoutSource.Token;
        var robotsPolicies = new Dictionary<RobotsAuthorityKey, string?>();
        var current = uri;

        try
        {
            for (var redirectCount = 0; ; redirectCount++)
            {
                await EnsureRobotsAllowedAsync(httpClient, current, robotsPolicies, options, token);

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
                using var response = await SendValidatedAsync(
                    httpClient,
                    request,
                    token);

                if (RedirectStatuses.Contains(response.StatusCode))
                {
                    if (redirectCount >= options.MaxRedirects)
                    {
                        throw Error(
                            "FETCH_REDIRECT_LIMIT",
                            "허용된 redirect 횟수를 초과했습니다.",
                            ExternalFetchFailureKind.POLICY);
                    }

                    current = ResolveRedirect(current, response.Headers.Location);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw Error(
                        "FETCH_REMOTE_RATE_LIMITED",
                        "외부 서버의 요청 제한에 도달했습니다. 잠시 후 수동으로 다시 시도하세요.",
                        ExternalFetchFailureKind.REMOTE);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw Error(
                        "FETCH_REMOTE_STATUS",
                        $"외부 서버가 HTTP {((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)} 상태를 반환했습니다.",
                        ExternalFetchFailureKind.REMOTE);
                }

                var mimeType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                if (mimeType is not ("text/html" or "application/xhtml+xml"))
                {
                    throw Error(
                        "FETCH_CONTENT_TYPE_UNSUPPORTED",
                        "HTML 문서만 가져올 수 있습니다.",
                        ExternalFetchFailureKind.POLICY);
                }

                if (response.Content.Headers.ContentLength is > 0 &&
                    response.Content.Headers.ContentLength > options.MaxHtmlBytes)
                {
                    throw TooLarge();
                }

                var bytes = await ReadBoundedAsync(response.Content, options.MaxHtmlBytes, token);
                var html = Decode(bytes, response.Content.Headers.ContentType?.CharSet);
                return new ExternalPageResponse(
                    current.AbsoluteUri,
                    (int)response.StatusCode,
                    mimeType,
                    bytes.LongLength,
                    Convert.ToHexString(SHA256.HashData(bytes)),
                    response.Headers.ETag?.ToString(),
                    response.Content.Headers.LastModified?.UtcDateTime,
                    html);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw Error(
                "FETCH_TIMEOUT",
                "외부 문서 가져오기가 총 제한 15초를 초과했습니다.",
                ExternalFetchFailureKind.TIMEOUT,
                exception);
        }
        catch (HttpRequestException exception)
        {
            var known = FindKnown(exception);
            if (known is not null)
            {
                throw known;
            }

            throw Error(
                "FETCH_REQUEST_FAILED",
                "외부 문서에 연결하지 못했습니다. 잠시 후 다시 시도하세요.",
                ExternalFetchFailureKind.REMOTE,
                exception);
        }
    }

    private async Task EnsureRobotsAllowedAsync(
        HttpClient client,
        Uri target,
        IDictionary<RobotsAuthorityKey, string?> policies,
        EffectiveOptions options,
        CancellationToken cancellationToken)
    {
        var key = RobotsAuthorityKey.From(target);
        if (!policies.TryGetValue(key, out var policyText))
        {
            policyText = await FetchRobotsPolicyAsync(client, key, options, cancellationToken);
            policies.Add(key, policyText);
        }

        if (policyText is not null && !robotsPolicy.IsAllowed(policyText, target))
        {
            throw Error(
                "FETCH_ROBOTS_DISALLOWED",
                "robots.txt에서 이 URL의 접근을 명시적으로 금지했습니다.",
                ExternalFetchFailureKind.POLICY);
        }
    }

    private async Task<string?> FetchRobotsPolicyAsync(
        HttpClient client,
        RobotsAuthorityKey authority,
        EffectiveOptions options,
        CancellationToken cancellationToken)
    {
        var current = new Uri(authority.Origin, "/robots.txt");
        try
        {
            for (var redirectCount = 0; ; redirectCount++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                using var response = await SendValidatedAsync(client, request, cancellationToken);

                if (RedirectStatuses.Contains(response.StatusCode))
                {
                    if (redirectCount >= options.MaxRobotsRedirects)
                    {
                        return null;
                    }

                    current = ResolveRedirect(current, response.Headers.Location);
                    continue;
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                var bytes = await ReadBoundedAsync(
                    response.Content,
                    options.MaxRobotsBytes,
                    cancellationToken);
                return Decode(bytes, response.Content.Headers.ContentType?.CharSet);
            }
        }
        catch (ExternalFetchException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendValidatedAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri
            ?? throw Error(
                "FETCH_URL_NOT_ALLOWED",
                "외부 요청 URL을 확인할 수 없습니다.",
                ExternalFetchFailureKind.POLICY);
        var addresses = await destinationValidator.ValidateAsync(requestUri, cancellationToken);
        request.Headers.ConnectionClose = true;
        using var scope = Pin(requestUri, addresses);
        return await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    public static async ValueTask<Stream> ConnectPinnedAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken) =>
        await ConnectPinnedAsync(context, cancellationToken, ConnectSocketAsync);

    internal static async ValueTask<Stream> ConnectPinnedAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken,
        Func<IPEndPoint, CancellationToken, ValueTask<Stream>> connector)
    {
        var pinned = CurrentPinnedDestination.Value;
        if (pinned is null ||
            !string.Equals(context.DnsEndPoint.Host, pinned.Host, StringComparison.OrdinalIgnoreCase) ||
            context.DnsEndPoint.Port != pinned.Port)
        {
            throw Error(
                "FETCH_PINNED_DESTINATION_MISSING",
                "검증된 원격 주소가 없어 연결을 중단했습니다.",
                ExternalFetchFailureKind.POLICY);
        }

        Exception? lastError = null;
        foreach (var address in pinned.Addresses)
        {
            try
            {
                return await connector(new IPEndPoint(address, pinned.Port), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
            }
        }

        throw Error(
            "FETCH_CONNECT_FAILED",
            "검증된 원격 주소에 연결하지 못했습니다.",
            ExternalFetchFailureKind.REMOTE,
            lastError);
    }

    private static async ValueTask<Stream> ConnectSocketAsync(
        IPEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static IDisposable Pin(Uri uri, IReadOnlyList<IPAddress> addresses)
    {
        var previous = CurrentPinnedDestination.Value;
        CurrentPinnedDestination.Value = new PinnedDestination(
            uri.IdnHost,
            uri.Port,
            addresses.ToArray());
        return new PinScope(previous);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Min(maxBytes, 64 * 1024));
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                throw TooLarge();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }

    private static string Decode(byte[] bytes, string? charset)
    {
        var encoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset.Trim('"', '\''));
            }
            catch (ArgumentException)
            {
                encoding = Encoding.UTF8;
            }
        }

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static Uri ResolveRedirect(Uri current, Uri? location)
    {
        if (location is null)
        {
            throw Error(
                "FETCH_REDIRECT_INVALID",
                "외부 서버가 유효하지 않은 redirect를 반환했습니다.",
                ExternalFetchFailureKind.REMOTE);
        }

        return location.IsAbsoluteUri ? location : new Uri(current, location);
    }

    private static EffectiveOptions Effective(ExternalFetchOptions options) => new(
        Math.Clamp(options.TotalTimeoutSeconds, 1, ExternalFetchOptions.ApprovedTotalTimeoutSeconds),
        Math.Clamp(options.MaxRedirects, 0, ExternalFetchOptions.ApprovedMaxRedirects),
        Math.Clamp(options.MaxRobotsRedirects, 0, ExternalFetchOptions.ApprovedMaxRobotsRedirects),
        Math.Clamp(options.MaxHtmlBytes, 1, ExternalFetchOptions.ApprovedMaxHtmlBytes),
        Math.Clamp(options.MaxRobotsBytes, 1, ExternalFetchOptions.ApprovedMaxRobotsBytes));

    private static ExternalFetchException TooLarge() => Error(
        "FETCH_RESPONSE_TOO_LARGE",
        "압축 해제된 HTML 크기는 2 MiB 이하여야 합니다.",
        ExternalFetchFailureKind.POLICY);

    private static ExternalFetchException? FindKnown(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is ExternalFetchException known)
            {
                return known;
            }
        }

        return null;
    }

    private static ExternalFetchException Error(
        string code,
        string message,
        ExternalFetchFailureKind kind,
        Exception? innerException = null) => new(code, message, kind, innerException);

    private sealed record EffectiveOptions(
        int TotalTimeoutSeconds,
        int MaxRedirects,
        int MaxRobotsRedirects,
        int MaxHtmlBytes,
        int MaxRobotsBytes);

    private sealed record PinnedDestination(
        string Host,
        int Port,
        IReadOnlyList<IPAddress> Addresses);

    private sealed class PinScope(PinnedDestination? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentPinnedDestination.Value = previous;
            _disposed = true;
        }
    }

    private readonly record struct RobotsAuthorityKey(string Scheme, string Host, int Port)
    {
        public Uri Origin => new UriBuilder(Scheme, Host, Port).Uri;

        public static RobotsAuthorityKey From(Uri uri) => new(
            uri.Scheme.ToLowerInvariant(),
            uri.IdnHost.ToLowerInvariant(),
            uri.Port);
    }
}
