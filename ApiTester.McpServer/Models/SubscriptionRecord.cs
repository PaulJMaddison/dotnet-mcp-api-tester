namespace ApiTester.McpServer.Models;

public sealed record SubscriptionRecord(
    Guid OrganisationId,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    bool Renews,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int ProjectsUsed,
    int RunsUsed,
    int AiCallsUsed,
    DateTime UpdatedUtc);
