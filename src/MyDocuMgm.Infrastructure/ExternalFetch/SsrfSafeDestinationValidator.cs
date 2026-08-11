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
    // IANA IPv6 Special-Purpose Address Registry snapshot reviewed 2026-08-11:
    // https://www.iana.org/assignments/iana-ipv6-special-registry/iana-ipv6-special-registry-1.csv
    // Longest-prefix evaluation is required because 2001::/23 contains explicitly
    // globally reachable exceptions. Transition mechanisms are intentionally denied.
    private static readonly Ipv6PolicyPrefix[] IanaSpecialPurposeIpv6Prefixes =
    new Ipv6PolicyPrefix[]
    {
        Prefix("::1", 128, false),
        Prefix("::", 128, false),
        Prefix("::ffff:0:0", 96, false),
        Prefix("64:ff9b::", 96, true),
        Prefix("64:ff9b:1::", 48, false),
        Prefix("100::", 64, false),
        Prefix("100:0:0:1::", 64, false),
        Prefix("2001::", 23, false),
        Prefix("2001::", 32, false),
        Prefix("2001:1::1", 128, true),
        Prefix("2001:1::2", 128, true),
        Prefix("2001:1::3", 128, true),
        Prefix("2001:2::", 48, false),
        Prefix("2001:3::", 32, true),
        Prefix("2001:4:112::", 48, true),
        Prefix("2001:10::", 28, false),
        Prefix("2001:20::", 28, true),
        Prefix("2001:30::", 28, true),
        Prefix("2001:db8::", 32, false),
        Prefix("2002::", 16, false),
        Prefix("2620:4f:8000::", 48, true),
        Prefix("3fff::", 20, false),
        Prefix("5f00::", 16, false),
        Prefix("fc00::", 7, false),
        Prefix("fe80::", 10, false),
    }.OrderByDescending(value => value.PrefixLength).ToArray();

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
            if (address.IsIPv6Multicast || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return false;
            }

            // RFC 6052 well-known NAT64 embeds an IPv4 destination in the final 32 bits.
            // It is only acceptable when that embedded destination passes the IPv4 policy.
            if (Contains(bytes, IPAddress.Parse("64:ff9b::").GetAddressBytes(), 96))
            {
                return IsPublic(new IPAddress(bytes[12..]));
            }

            foreach (var prefix in IanaSpecialPurposeIpv6Prefixes)
            {
                if (Contains(bytes, prefix.Network, prefix.PrefixLength))
                {
                    return prefix.GloballyReachable;
                }
            }

            return true;
        }

        return false;
    }

    private static ExternalFetchException Policy(string code, string message) =>
        new(code, message, ExternalFetchFailureKind.POLICY);

    private static Ipv6PolicyPrefix Prefix(string address, int prefixLength, bool globallyReachable) =>
        new(IPAddress.Parse(address).GetAddressBytes(), prefixLength, globallyReachable);

    private static bool Contains(byte[] address, byte[] network, int prefixLength)
    {
        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        for (var index = 0; index < wholeBytes; index++)
        {
            if (address[index] != network[index]) return false;
        }

        if (remainingBits == 0) return true;
        var mask = (byte)(0xFF << (8 - remainingBits));
        return (address[wholeBytes] & mask) == (network[wholeBytes] & mask);
    }

    private readonly record struct Ipv6PolicyPrefix(
        byte[] Network,
        int PrefixLength,
        bool GloballyReachable);
}
