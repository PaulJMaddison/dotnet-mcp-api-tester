using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface ITestRunStore
{
    Task SaveAsync(TestRunRecord record);

    Task<TestRunRecord?> GetAsync(Guid runId);

    Task<IReadOnlyList<TestRunRecord>> ListAsync(string projectKey, int take, string? operationId = null);
}

