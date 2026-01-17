using System.Net;
using System.Net.Sockets;

namespace ApiTester.McpServer.Services;

public sealed class SsrfGuard
{
    public async Task<(bool Allowed, string? Reason)> CheckAsync(
        Uri uri,
        bool blockLocalhost,
        bool blockPrivateNetworks,
        CancellationToken ct)
    {
        var host = uri.Host;

        // 1) Obvious hostname blocks
        if (blockLocalhost && string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return (false, "Blocked host: localhost");

        // 2) If host is an IP, validate directly
        if (IPAddress.TryParse(host, out var ip))
        {
            var ok = IsIpAllowed(ip, blockLocalhost, blockPrivateNetworks, out var reason);
            return (ok, reason);
        }

        // 3) DNS resolve hostname and validate all resolved IPs
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (Exception ex)
        {
            return (false, $"DNS resolution failed for host '{host}': {ex.Message}");
        }

        if (addresses.Length == 0)
            return (false, $"DNS resolution returned no addresses for host '{host}'.");

        foreach (var addr in addresses)
        {
            var ok = IsIpAllowed(addr, blockLocalhost, blockPrivateNetworks, out var reason);
            if (!ok)
                return (false, $"Host '{host}' resolves to blocked IP {addr}: {reason}");
        }

        return (true, null);
    }

    private static bool IsIpAllowed(IPAddress ip, bool blockLocalhost, bool blockPrivate, out string? reason)
    {
        reason = null;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
        {
            return IsIpAllowed(ip.MapToIPv4(), blockLocalhost, blockPrivate, out reason);
        }

        // Localhost / loopback
        if (blockLocalhost && IPAddress.IsLoopback(ip))
        {
            reason = "Loopback address";
            return false;
        }

        // Link-local IPv4 (includes metadata 169.254.169.254)
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();

            // 0.0.0.0/8 (unspecified)
            if (b[0] == 0)
            {
                reason = "Unspecified IPv4 address (0.0.0.0/8)";
                return false;
            }

            // 169.254.0.0/16
            if (b[0] == 169 && b[1] == 254)
            {
                reason = "Link-local IPv4 (includes metadata endpoint range)";
                return false;
            }

            if (blockPrivate)
            {
                // 10.0.0.0/8
                if (b[0] == 10)
                {
                    reason = "Private IPv4 (10.0.0.0/8)";
                    return false;
                }

                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                {
                    reason = "Private IPv4 (172.16.0.0/12)";
                    return false;
                }

                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168)
                {
                    reason = "Private IPv4 (192.168.0.0/16)";
                    return false;
                }
            }
        }

        // IPv6 checks
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Loopback ::1 handled above by IsLoopback, but keep policy consistent
            if (blockLocalhost && IPAddress.IsLoopback(ip))
            {
                reason = "Loopback IPv6";
                return false;
            }

            var bytes = ip.GetAddressBytes();

            // fe80::/10 link-local
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                reason = "Link-local IPv6 (fe80::/10)";
                return false;
            }

            if (blockPrivate)
            {
                // fc00::/7 unique local addresses (ULA)
                if ((bytes[0] & 0xFE) == 0xFC)
                {
                    reason = "Private IPv6 (fc00::/7)";
                    return false;
                }
            }
        }

        return true;
    }
}
