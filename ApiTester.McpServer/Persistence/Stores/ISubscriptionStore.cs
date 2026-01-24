using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed record SubscriptionUsageLimits(
    int? MaxProjects,
    int? MaxRunsPerPeriod,
    int? MaxAiCallsPerPeriod);

public sealed record SubscriptionUsageUpdate(int ProjectsDelta, int RunsDelta, int AiCallsDelta);

public interface ISubscriptionStore
{
    Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct);
    Task<SubscriptionRecord?> TryConsumeAsync(
        Guid organisationId,
        SubscriptionUsageUpdate update,
        SubscriptionUsageLimits limits,
        DateTime nowUtc,
        CancellationToken ct);
    Task<SubscriptionRecord?> UpdateProjectsUsedAsync(Guid organisationId, int projectsUsed, DateTime nowUtc, CancellationToken ct);
}
