using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IUserStore
{
    Task<UserRecord> CreateAsync(string externalId, string displayName, string? email, CancellationToken ct);
    Task<UserRecord?> GetAsync(Guid userId, CancellationToken ct);
    Task<UserRecord?> GetByExternalIdAsync(string externalId, CancellationToken ct);
}
