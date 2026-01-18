using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IProjectStore
{
    Task<ProjectRecord> CreateAsync(Guid organisationId, string ownerKey, string name, CancellationToken ct);
    Task<PagedResult<ProjectRecord>> ListAsync(Guid organisationId, PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct);
    Task<ProjectRecord?> GetAsync(Guid organisationId, Guid projectId, CancellationToken ct);
    Task<ProjectRecord?> GetByKeyAsync(Guid organisationId, string projectKey, CancellationToken ct);
    Task<bool> ExistsAsync(Guid projectId, CancellationToken ct);
}
