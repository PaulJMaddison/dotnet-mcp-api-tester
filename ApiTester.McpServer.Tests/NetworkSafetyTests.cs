using ApiTester.McpServer.Services;

namespace ApiTester.McpServer.Tests;

public sealed class NetworkSafetyTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5055", true, true, false)]
    [InlineData("http://127.0.0.1:5055", false, true, true)]
    [InlineData("http://10.0.0.1", true, true, false)]
    [InlineData("http://10.0.0.1", true, false, true)]
    [InlineData("http://172.16.0.1", true, true, false)]
    [InlineData("http://172.31.255.255", true, true, false)]
    [InlineData("http://172.32.0.1", true, true, true)]
    [InlineData("http://192.168.1.1", true, true, false)]
    [InlineData("http://192.168.1.1", true, false, true)]
    [InlineData("http://169.254.169.254", false, false, false)]
    [InlineData("http://0.0.0.0", false, false, false)]
    [InlineData("http://224.0.0.1", false, false, false)]
    [InlineData("http://[::1]:5055", true, true, false)]
    [InlineData("http://[::1]:5055", false, true, true)]
    [InlineData("http://[fc00::1]", false, true, false)]
    [InlineData("http://[fc00::1]", false, false, true)]
    [InlineData("http://[fe80::1]", false, false, false)]
    [InlineData("http://[ff02::1]", false, false, false)]
    [InlineData("http://[::ffff:127.0.0.1]", true, true, false)]
    public async Task GuardAppliesNetworkPolicyAcrossAddressFamilies(
        string url,
        bool blockLocalhost,
        bool blockPrivate,
        bool expectedAllowed)
    {
        var (allowed, _) = await new SsrfGuard().CheckAsync(new Uri(url), blockLocalhost, blockPrivate, CancellationToken.None);
        Assert.Equal(expectedAllowed, allowed);
    }

    [Fact]
    public async Task GuardAllowsLocalhostOnlyWhenOperatorPolicyAllowsIt()
    {
        var guard = new SsrfGuard();
        var blocked = await guard.CheckAsync(new Uri("http://localhost:5055"), true, true, CancellationToken.None);
        var allowed = await guard.CheckAsync(new Uri("http://localhost:5055"), false, false, CancellationToken.None);
        Assert.False(blocked.Allowed);
        Assert.True(allowed.Allowed);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://metadata/")]
    [InlineData("http://metadata.google.internal/")]
    [InlineData("http://metadata.azure.internal/")]
    [InlineData("http://service.local/")]
    public async Task MetadataAndLocalDiscoveryTargetsRemainBlockedEvenWhenPrivateNetworksAreAllowed(string url)
    {
        var result = await new SsrfGuard().CheckAsync(new Uri(url), false, false, CancellationToken.None);
        Assert.False(result.Allowed);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task CancellationIsObservedDuringDnsResolutionPath()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SsrfGuard().CheckAsync(new Uri("http://example.invalid"), true, true, cts.Token));
    }
}
