using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IProjectStore
{
    Task<ProjectRecord> CreateAsync(Guid tenantId, string ownerKey, string name, CancellationToken ct);
    Task<PagedResult<ProjectRecord>> ListAsync(Guid tenantId, PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct);
    Task<ProjectRecord?> GetAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<ProjectRecord?> GetByKeyAsync(Guid tenantId, string projectKey, CancellationToken ct);
    Task<bool> ExistsAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<bool> ExistsAnyAsync(Guid projectId, CancellationToken ct);
}
