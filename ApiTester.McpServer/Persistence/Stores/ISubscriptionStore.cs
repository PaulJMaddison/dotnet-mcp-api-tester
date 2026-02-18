using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed record SubscriptionUsageLimits(
    int? MaxProjects,
    int? MaxRunsPerPeriod,
    int? MaxAiCallsPerPeriod,
    int? MaxExportsPerPeriod = null);

public sealed record SubscriptionUsageUpdate(int ProjectsDelta, int RunsDelta, int AiCallsDelta, int ExportsDelta = 0);

public interface ISubscriptionStore
{
    Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct);
    Task<SubscriptionRecord> UpsertStripeAsync(
        Guid organisationId,
        Guid tenantId,
        SubscriptionPlan plan,
        SubscriptionStatus status,
        bool renews,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime nowUtc,
        CancellationToken ct);

    Task<UsageCounterRecord> GetOrCreateUsageAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct);
    Task<UsageCounterRecord?> TryConsumeAsync(
        Guid tenantId,
        SubscriptionUsageUpdate update,
        SubscriptionUsageLimits limits,
        DateTime nowUtc,
        CancellationToken ct);
    Task<UsageCounterRecord?> UpdateProjectsUsedAsync(Guid tenantId, int projectsUsed, DateTime nowUtc, CancellationToken ct);
}
