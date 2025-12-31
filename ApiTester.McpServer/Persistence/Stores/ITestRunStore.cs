using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface ITestRunStore
{
    Task SaveAsync(TestRunRecord record);

    Task<TestRunRecord?> GetAsync(Guid runId);

    Task<PagedResult<TestRunRecord>> ListAsync(
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null);
}
