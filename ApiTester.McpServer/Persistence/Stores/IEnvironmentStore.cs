using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IEnvironmentStore
{
    Task<EnvironmentRecord> CreateAsync(string ownerKey, Guid projectId, string name, string baseUrl, CancellationToken ct);
    Task<IReadOnlyList<EnvironmentRecord>> ListAsync(string ownerKey, Guid projectId, CancellationToken ct);
    Task<EnvironmentRecord?> GetAsync(string ownerKey, Guid projectId, Guid environmentId, CancellationToken ct);
    Task<EnvironmentRecord?> GetByNameAsync(string ownerKey, Guid projectId, string name, CancellationToken ct);
    Task<EnvironmentRecord?> UpdateAsync(string ownerKey, Guid projectId, Guid environmentId, string name, string baseUrl, CancellationToken ct);
    Task<bool> DeleteAsync(string ownerKey, Guid projectId, Guid environmentId, CancellationToken ct);
}
