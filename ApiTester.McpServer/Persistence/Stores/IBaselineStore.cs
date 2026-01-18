using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IBaselineStore
{
    Task<BaselineRecord?> GetAsync(Guid organisationId, string projectKey, string operationId, CancellationToken ct);
    Task<IReadOnlyList<BaselineRecord>> ListAsync(Guid organisationId, string? projectKey, string? operationId, int take, CancellationToken ct);
    Task<BaselineRecord?> SetAsync(Guid organisationId, string projectKey, string operationId, Guid runId, CancellationToken ct);
}
