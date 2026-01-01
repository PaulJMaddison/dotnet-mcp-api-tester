using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IRunAnnotationStore
{
    Task<IReadOnlyList<RunAnnotationRecord>> ListAsync(string ownerKey, Guid runId, CancellationToken ct);
    Task<RunAnnotationRecord?> GetAsync(string ownerKey, Guid runId, Guid annotationId, CancellationToken ct);
    Task<RunAnnotationRecord> CreateAsync(string ownerKey, Guid runId, string note, string? jiraLink, CancellationToken ct);
    Task<RunAnnotationRecord?> UpdateAsync(string ownerKey, Guid runId, Guid annotationId, string note, string? jiraLink, CancellationToken ct);
    Task<bool> DeleteAsync(string ownerKey, Guid runId, Guid annotationId, CancellationToken ct);
}
