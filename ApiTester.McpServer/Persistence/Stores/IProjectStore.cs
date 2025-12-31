using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IProjectStore
{
    Task<ProjectRecord> CreateAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<ProjectRecord>> ListAsync(int take, CancellationToken ct);
    Task<ProjectRecord?> GetAsync(Guid projectId, CancellationToken ct);
}
