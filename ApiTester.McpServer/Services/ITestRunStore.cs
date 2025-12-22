using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Services;

public interface ITestRunStore
{
    Task SaveAsync(TestRunRecord record, CancellationToken ct = default);
    Task<TestRunRecord?> GetAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<TestRunRecord>> ListAsync(int take = 50, CancellationToken ct = default);
}
