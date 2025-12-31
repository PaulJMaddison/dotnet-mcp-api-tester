using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface ITestRunStore
{
    Task SaveAsync(TestRunRecord record);

    Task<TestRunRecord?> GetAsync(string ownerKey, Guid runId);

    Task<PagedResult<TestRunRecord>> ListAsync(
        string ownerKey,
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null);
}
