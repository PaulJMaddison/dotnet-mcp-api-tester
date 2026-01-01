using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IOpenApiSpecStore
{
    Task<OpenApiSpecRecord?> GetAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<OpenApiSpecRecord>> ListAsync(Guid projectId, CancellationToken ct);
    Task<OpenApiSpecRecord?> GetByIdAsync(Guid specId, CancellationToken ct);
    Task<OpenApiSpecRecord> UpsertAsync(Guid projectId, string title, string version, string specJson, string specHash, DateTime createdUtc, CancellationToken ct);
}
