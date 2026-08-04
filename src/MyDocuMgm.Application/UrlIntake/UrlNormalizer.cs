using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MyDocuMgm.Domain;

namespace MyDocuMgm.Application.UrlIntake;

public sealed record NormalizedUrlResult(
    string OriginalUrl,
    string NormalizedUrl,
    ContentSourceKind SourceKind);

public static class UrlNormalizer
{
    private static readonly HashSet<string> InstagramHosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "instagram.com",
            "www.instagram.com",
            "m.instagram.com"
        };

    private static readonly HashSet<string> InstagramContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "p", "reel", "tv" };

    public static NormalizedUrlResult Normalize(string? input)
    {
        var original = input?.Trim() ?? string.Empty;
        if (original.Length == 0)
        {
            throw Invalid("URL_REQUIRED", "URL을 입력해 주세요.");
        }

        if (original.Length > 2048)
        {
            throw Invalid("URL_TOO_LONG", "URL은 2,048자 이하여야 합니다.");
        }

        if (original.Any(char.IsControl) ||
            !Uri.TryCreate(original, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw Invalid("URL_INVALID", "absolute http 또는 https URL을 입력해 주세요.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            throw Invalid("URL_SCHEME_NOT_ALLOWED", "http 또는 https URL만 입력할 수 있습니다.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw Invalid("URL_USER_INFO_NOT_ALLOWED", "사용자 정보가 포함된 URL은 입력할 수 없습니다.");
        }

        return InstagramHosts.Contains(uri.IdnHost)
            ? NormalizeInstagram(original, uri)
            : NormalizeGeneric(original, uri);
    }

    public static byte[] ComputeHash(string normalizedUrl) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl));

    private static NormalizedUrlResult NormalizeGeneric(string original, Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        var host = FormatHost(uri).ToLowerInvariant();
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port.ToString(CultureInfo.InvariantCulture)}";
        var path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
        var query = uri.Query;
        var normalizedUrl = $"{scheme}://{host}{port}{path}{query}";
        if (normalizedUrl.Length > 2048)
        {
            throw Invalid("URL_TOO_LONG", "정규화 URL은 2,048자 이하여야 합니다.");
        }

        return new NormalizedUrlResult(
            original,
            normalizedUrl,
            ContentSourceKind.GENERIC);
    }

    private static NormalizedUrlResult NormalizeInstagram(string original, Uri uri)
    {
        if (!uri.IsDefaultPort)
        {
            throw Invalid(
                "INSTAGRAM_URL_NOT_SUPPORTED",
                "기본 포트를 사용하는 Instagram 콘텐츠 URL만 지원합니다.");
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 ||
            !InstagramContentTypes.Contains(segments[0]) ||
            !IsInstagramShortcode(segments[1]))
        {
            throw Invalid(
                "INSTAGRAM_URL_NOT_SUPPORTED",
                "Instagram 게시물, 릴스 또는 TV 콘텐츠 URL만 지원합니다.");
        }

        var contentType = segments[0].ToLowerInvariant();
        return new NormalizedUrlResult(
            original,
            $"https://www.instagram.com/{contentType}/{segments[1]}/",
            ContentSourceKind.INSTAGRAM);
    }

    private static string FormatHost(Uri uri) =>
        uri.HostNameType == UriHostNameType.IPv6 ? $"[{uri.Host}]" : uri.IdnHost;

    private static bool IsInstagramShortcode(string value) =>
        value.Length > 0 && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static DomainRuleException Invalid(string code, string message) => new(code, message);
}
