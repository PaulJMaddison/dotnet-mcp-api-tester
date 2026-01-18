using ApiTester.McpServer.Models;
using ApiTester.Web.Auth;

namespace ApiTester.Web.UnitTests;

public sealed class ApiKeyAccessEvaluatorTests
{
    [Fact]
    public void IsActive_ReturnsFalse_WhenExpired()
    {
        var record = BuildRecord(expiresUtc: DateTime.UtcNow.AddMinutes(-1), revokedUtc: null);

        Assert.False(ApiKeyAccessEvaluator.IsActive(record, DateTime.UtcNow));
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenRevoked()
    {
        var record = BuildRecord(expiresUtc: null, revokedUtc: DateTime.UtcNow.AddMinutes(-1));

        Assert.False(ApiKeyAccessEvaluator.IsActive(record, DateTime.UtcNow));
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenValid()
    {
        var record = BuildRecord(expiresUtc: DateTime.UtcNow.AddHours(1), revokedUtc: null);

        Assert.True(ApiKeyAccessEvaluator.IsActive(record, DateTime.UtcNow));
    }

    private static ApiKeyRecord BuildRecord(DateTime? expiresUtc, DateTime? revokedUtc)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test",
            ApiKeyScopes.ProjectsRead,
            expiresUtc,
            revokedUtc,
            "hash",
            "prefix");
}
