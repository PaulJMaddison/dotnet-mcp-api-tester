using ApiTester.McpServer.Services;

namespace ApiTester.McpServer.Tests;

public sealed class NetworkSafetyTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5055", true, true, false)]
    [InlineData("http://10.0.0.1", true, true, false)]
    [InlineData("http://169.254.169.254", false, true, false)]
    public async Task Guard_BlocksUnsafeTargets(string url, bool blockLocalhost, bool blockPrivate, bool expectedAllowed)
    {
        var (allowed, _) = await new SsrfGuard().CheckAsync(new Uri(url), blockLocalhost, blockPrivate, CancellationToken.None);
        Assert.Equal(expectedAllowed, allowed);
    }

    [Fact]
    public async Task Guard_AllowsLocalhostOnlyWhenOperatorPolicyAllowsIt()
    {
        var guard = new SsrfGuard();
        var blocked = await guard.CheckAsync(new Uri("http://localhost:5055"), true, true, CancellationToken.None);
        var allowed = await guard.CheckAsync(new Uri("http://localhost:5055"), false, false, CancellationToken.None);
        Assert.False(blocked.Allowed);
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public async Task Guard_AlwaysBlocksCloudMetadataLinkLocalAddress()
    {
        var result = await new SsrfGuard().CheckAsync(new Uri("http://169.254.169.254/latest/meta-data"), false, false, CancellationToken.None);
        Assert.False(result.Allowed);
        Assert.Contains("Link-local", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
