using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface ITestRunStore
{
    Task SaveAsync(TestRunRecord record);

    Task<TestRunRecord?> GetAsync(Guid organisationId, Guid runId);

    Task<bool> SetBaselineAsync(Guid organisationId, Guid runId, Guid baselineRunId);

    Task<PagedResult<TestRunRecord>> ListAsync(
        Guid organisationId,
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null);
}
