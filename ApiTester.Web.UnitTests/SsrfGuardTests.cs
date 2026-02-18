using ApiTester.McpServer.Services;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class SsrfGuardTests
{
    [Theory]
    [InlineData("http://localhost", "localhost")]
    [InlineData("http://127.0.0.1", "Loopback")]
    [InlineData("http://0.0.0.0", "0.0.0.0")]
    [InlineData("http://10.0.0.1", "Private")]
    [InlineData("http://192.168.0.1", "Private")]
    [InlineData("http://172.16.0.1", "Private")]
    [InlineData("http://169.254.1.1", "Link-local")]
    [InlineData("http://[::1]", "Loopback")]
    [InlineData("http://[::ffff:127.0.0.1]", "Loopback")]
    [InlineData("http://[::ffff:10.0.0.1]", "Private")]
    public async Task CheckAsync_BlocksRestrictedHosts(string url, string reasonFragment)
    {
        var guard = new SsrfGuard();

        var (allowed, reason) = await guard.CheckAsync(
            new Uri(url),
            blockLocalhost: true,
            blockPrivateNetworks: true,
            ct: CancellationToken.None);

        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Contains(reasonFragment, reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://service.local", ".local")]
    [InlineData("http://metadata.google.internal", "Blocked host")]
    [InlineData("http://224.0.0.1", "Multicast")]
    [InlineData("http://[ff02::1]", "Multicast")]
    public async Task CheckAsync_BlocksAdditionalSaasRestrictedHosts(string url, string reasonFragment)
    {
        var guard = new SsrfGuard();

        var (allowed, reason) = await guard.CheckAsync(
            new Uri(url),
            blockLocalhost: true,
            blockPrivateNetworks: true,
            ct: CancellationToken.None);

        Assert.False(allowed);
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Contains(reasonFragment, reason!, StringComparison.OrdinalIgnoreCase);
    }
}
