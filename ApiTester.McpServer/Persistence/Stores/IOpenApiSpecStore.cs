using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IOpenApiSpecStore
{
    Task<OpenApiSpecRecord?> GetAsync(Guid projectId, CancellationToken ct);
    Task<OpenApiSpecRecord> UpsertAsync(Guid projectId, string title, string version, string specJson, DateTime createdUtc, CancellationToken ct);
}
