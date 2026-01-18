namespace ApiTester.Web.AI;

public sealed class AiRateLimitOptions
{
    public int Capacity { get; init; } = 10;
    public int RefillTokensPerMinute { get; init; } = 10;
}
