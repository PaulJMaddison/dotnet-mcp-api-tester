using ApiTester.Web.AbuseProtection;
using Microsoft.Extensions.Options;

namespace ApiTester.Web.UnitTests;

public class TenantIpRateLimiterTests
{
    [Fact]
    public void AiCategory_UsesStricterLimitThanDefault()
    {
        var options = Options.Create(new AbuseRateLimitOptions
        {
            Default = new EndpointRateLimitPolicy(5, 5),
            RunExecution = new EndpointRateLimitPolicy(3, 3),
            Ai = new EndpointRateLimitPolicy(1, 1)
        });

        var limiter = new TenantIpRateLimiter(options, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var tenantId = Guid.NewGuid();

        var firstAiAllowed = limiter.TryConsume(tenantId, "127.0.0.1", EndpointCategory.Ai, out _);
        var secondAiAllowed = limiter.TryConsume(tenantId, "127.0.0.1", EndpointCategory.Ai, out var aiRetry);
        var defaultAllowed = limiter.TryConsume(tenantId, "127.0.0.1", EndpointCategory.Default, out _);

        Assert.True(firstAiAllowed);
        Assert.False(secondAiAllowed);
        Assert.True(aiRetry > TimeSpan.Zero);
        Assert.True(defaultAllowed);
    }

    [Fact]
    public void DifferentIps_HaveSeparateBucketsForSameTenantAndCategory()
    {
        var options = Options.Create(new AbuseRateLimitOptions
        {
            Default = new EndpointRateLimitPolicy(1, 1),
            RunExecution = new EndpointRateLimitPolicy(1, 1),
            Ai = new EndpointRateLimitPolicy(1, 1)
        });

        var limiter = new TenantIpRateLimiter(options, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var tenantId = Guid.NewGuid();

        var firstIpAllowed = limiter.TryConsume(tenantId, "10.0.0.1", EndpointCategory.RunExecution, out _);
        var secondIpAllowed = limiter.TryConsume(tenantId, "10.0.0.2", EndpointCategory.RunExecution, out _);

        Assert.True(firstIpAllowed);
        Assert.True(secondIpAllowed);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
