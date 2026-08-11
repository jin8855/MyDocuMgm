using System.Net;
using System.Net.Sockets;
using MyDocuMgm.Application.ExternalFetch;

namespace MyDocuMgm.Infrastructure.ExternalFetch;

public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken);
}

public sealed class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}

public sealed class SsrfSafeDestinationValidator(IDnsResolver dnsResolver)
{
    public async Task<IReadOnlyList<IPAddress>> ValidateAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
        {
            throw Policy("FETCH_URL_NOT_ALLOWED", "http 또는 https 공개 URL만 사용할 수 있습니다.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw Policy("FETCH_URL_USER_INFO_NOT_ALLOWED", "사용자 정보가 포함된 URL은 사용할 수 없습니다.");
        }

        var expectedPort = uri.Scheme == "https" ? 443 : 80;
        if (uri.Port != expectedPort)
        {
            throw Policy("FETCH_PORT_NOT_ALLOWED", "외부 URL 포트는 80 또는 443만 사용할 수 있습니다.");
        }

        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 ||
            IPAddress.TryParse(uri.Host, out _))
        {
            throw Policy("FETCH_IP_LITERAL_NOT_ALLOWED", "IP 주소를 직접 사용한 URL은 사용할 수 없습니다.");
        }

        return await ResolvePublicAddressesAsync(uri.IdnHost, cancellationToken);
    }

    public async Task<IReadOnlyList<IPAddress>> ResolvePublicAddressesAsync(
        string host,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await dnsResolver.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ExternalFetchException(
                "FETCH_DNS_FAILED",
                "외부 호스트의 주소를 확인하지 못했습니다.",
                ExternalFetchFailureKind.REMOTE,
                exception);
        }

        if (addresses.Length == 0)
        {
            throw new ExternalFetchException(
                "FETCH_DNS_FAILED",
                "외부 호스트의 주소를 확인하지 못했습니다.",
                ExternalFetchFailureKind.REMOTE);
        }

        if (addresses.Any(address => !IsPublic(address)))
        {
            throw Policy(
                "FETCH_DNS_NOT_PUBLIC",
                "내부 또는 예약 네트워크로 연결되는 URL은 사용할 수 없습니다.");
        }

        return addresses;
    }

    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (context.DnsEndPoint.Port is not (80 or 443))
        {
            throw Policy("FETCH_PORT_NOT_ALLOWED", "외부 URL 포트는 80 또는 443만 사용할 수 있습니다.");
        }

        var addresses = await ResolvePublicAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, context.DnsEndPoint.Port),
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
                socket.Dispose();
            }
        }

        throw new ExternalFetchException(
            "FETCH_CONNECT_FAILED",
            "검증된 외부 주소에 연결하지 못했습니다.",
            ExternalFetchFailureKind.REMOTE,
            lastError);
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(
                bytes[0] == 0 ||
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) ||
                bytes[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(
                address.IsIPv6LinkLocal ||
                address.IsIPv6Multicast ||
                address.IsIPv6SiteLocal ||
                (bytes[0] & 0xFE) == 0xFC ||
                (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8));
        }

        return false;
    }

    private static ExternalFetchException Policy(string code, string message) =>
        new(code, message, ExternalFetchFailureKind.POLICY);
}
