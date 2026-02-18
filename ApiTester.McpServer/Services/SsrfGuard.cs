using System.Net;
using System.Net.Sockets;

namespace ApiTester.McpServer.Services;

public sealed class SsrfGuard
{
    private static readonly string[] BlockedHostSuffixes = [".local"];
    private static readonly HashSet<string> BlockedHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "metadata",
        "metadata.google.internal",
        "metadata.azure.internal"
    };

    public async Task<(bool Allowed, string? Reason)> CheckAsync(
        Uri uri,
        bool blockLocalhost,
        bool blockPrivateNetworks,
        CancellationToken ct)
    {
        var host = uri.Host.Trim();

        if (IsBlockedHostname(host, blockLocalhost, out var hostnameReason))
            return (false, hostnameReason);

        if (IPAddress.TryParse(host, out var ip))
        {
            var ok = IsIpAllowed(ip, blockLocalhost, blockPrivateNetworks, out var reason);
            return (ok, reason);
        }

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

    private static bool IsBlockedHostname(string host, bool blockLocalhost, out string? reason)
    {
        reason = null;

        if (host.Length == 0)
        {
            reason = "Host is empty.";
            return true;
        }

        if (BlockedHostnames.Contains(host))
        {
            reason = $"Blocked host: {host}";
            return true;
        }

        foreach (var suffix in BlockedHostSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Blocked host suffix: {suffix}";
                return true;
            }
        }

        if (blockLocalhost && string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Blocked host: localhost";
            return true;
        }

        return false;
    }

    private static bool IsIpAllowed(IPAddress ip, bool blockLocalhost, bool blockPrivate, out string? reason)
    {
        reason = null;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
            return IsIpAllowed(ip.MapToIPv4(), blockLocalhost, blockPrivate, out reason);

        if (IPAddress.IsLoopback(ip))
        {
            if (blockLocalhost)
            {
                reason = "Loopback address";
                return false;
            }

            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();

            if (b[0] == 0)
            {
                reason = "Unspecified IPv4 address (0.0.0.0/8)";
                return false;
            }

            if (b[0] == 169 && b[1] == 254)
            {
                reason = "Link-local IPv4 (includes metadata endpoint range)";
                return false;
            }

            if (b[0] >= 224 && b[0] <= 239)
            {
                reason = "Multicast IPv4 (224.0.0.0/4)";
                return false;
            }

            if (blockPrivate)
            {
                if (b[0] == 10)
                {
                    reason = "Private IPv4 (10.0.0.0/8)";
                    return false;
                }

                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                {
                    reason = "Private IPv4 (172.16.0.0/12)";
                    return false;
                }

                if (b[0] == 192 && b[1] == 168)
                {
                    reason = "Private IPv4 (192.168.0.0/16)";
                    return false;
                }
            }
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = ip.GetAddressBytes();

            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                reason = "Link-local IPv6 (fe80::/10)";
                return false;
            }

            if (bytes[0] == 0xFF)
            {
                reason = "Multicast IPv6 (ff00::/8)";
                return false;
            }

            if (blockPrivate && (bytes[0] & 0xFE) == 0xFC)
            {
                reason = "Private IPv6 (fc00::/7)";
                return false;
            }
        }

        return true;
    }
}
