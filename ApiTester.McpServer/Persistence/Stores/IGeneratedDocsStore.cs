using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IGeneratedDocsStore
{
    Task<GeneratedDocsRecord?> GetAsync(Guid organisationId, Guid projectId, CancellationToken ct);

    Task<GeneratedDocsRecord> UpsertAsync(
        Guid organisationId,
        Guid projectId,
        Guid specId,
        string docsJson,
        DateTime generatedUtc,
        CancellationToken ct);
}
