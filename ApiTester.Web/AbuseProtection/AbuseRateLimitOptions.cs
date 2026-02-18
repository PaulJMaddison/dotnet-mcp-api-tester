namespace ApiTester.Web.AbuseProtection;

public sealed class AbuseRateLimitOptions
{
    public EndpointRateLimitPolicy Default { get; set; } = new(120, 120);

    public EndpointRateLimitPolicy RunExecution { get; set; } = new(30, 30);

    public EndpointRateLimitPolicy Ai { get; set; } = new(10, 10);
}

public sealed record EndpointRateLimitPolicy(int Capacity, int RefillTokensPerMinute);
