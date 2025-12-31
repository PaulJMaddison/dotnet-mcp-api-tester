using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface ITestPlanStore
{
    Task<TestPlanRecord?> GetAsync(Guid projectId, string operationId, CancellationToken ct);
    Task<TestPlanRecord> UpsertAsync(Guid projectId, string operationId, string planJson, DateTime createdUtc, CancellationToken ct);
}
