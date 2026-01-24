using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface ITestRunStore
{
    Task SaveAsync(TestRunRecord record);

    Task<TestRunRecord?> GetAsync(Guid tenantId, Guid runId);

    Task<bool> SetBaselineAsync(Guid tenantId, Guid runId, Guid baselineRunId);

    Task<PagedResult<TestRunRecord>> ListAsync(
        Guid tenantId,
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null,
        DateTimeOffset? notBeforeUtc = null);

    Task<int> PruneAsync(Guid tenantId, DateTimeOffset cutoffUtc, CancellationToken ct);
}
