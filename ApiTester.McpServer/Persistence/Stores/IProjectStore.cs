using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IProjectStore
{
    Task<ProjectRecord> CreateAsync(string name, CancellationToken ct);
    Task<PagedResult<ProjectRecord>> ListAsync(PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct);
    Task<ProjectRecord?> GetAsync(Guid projectId, CancellationToken ct);
}
