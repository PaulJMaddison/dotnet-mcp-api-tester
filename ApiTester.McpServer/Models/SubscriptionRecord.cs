namespace ApiTester.McpServer.Models;

public sealed record SubscriptionRecord(
    Guid OrganisationId,
    Guid TenantId,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    bool Renews,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateTime UpdatedUtc);
