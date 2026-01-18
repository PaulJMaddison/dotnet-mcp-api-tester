using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IOpenApiSpecStore
{
    Task<OpenApiSpecRecord?> GetAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<OpenApiSpecRecord>> ListAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<OpenApiSpecRecord?> GetByIdAsync(Guid tenantId, Guid specId, CancellationToken ct);
    Task<OpenApiSpecRecord> UpsertAsync(Guid tenantId, Guid projectId, string title, string version, string specJson, string specHash, DateTime createdUtc, CancellationToken ct);
    Task<bool> DeleteAsync(Guid tenantId, Guid specId, CancellationToken ct);
}
