using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IProjectStore
{
    Task<ProjectRecord> CreateAsync(string ownerKey, string name, CancellationToken ct);
    Task<PagedResult<ProjectRecord>> ListAsync(string ownerKey, PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct);
    Task<ProjectRecord?> GetAsync(string ownerKey, Guid projectId, CancellationToken ct);
    Task<ProjectRecord?> GetByKeyAsync(string ownerKey, string projectKey, CancellationToken ct);
    Task<bool> ExistsAsync(Guid projectId, CancellationToken ct);
}
