using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IRunStore
{
    Task SaveAsync(Guid tenantId, Guid projectId, Guid runId, string operationId, DateTime startedUtc, DateTime completedUtc, TestRunResult result, CancellationToken ct);
    Task<object?> GetAsync(Guid tenantId, Guid runId, CancellationToken ct);
    Task<object> ListAsync(Guid tenantId, Guid? projectId, int take, CancellationToken ct);
}
