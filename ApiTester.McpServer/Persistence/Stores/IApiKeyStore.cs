using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IApiKeyStore
{
    Task<ApiKeyRecord> CreateAsync(
        Guid organisationId,
        Guid userId,
        string name,
        string scopes,
        DateTime? expiresUtc,
        string hash,
        string prefix,
        CancellationToken ct);

    Task<IReadOnlyList<ApiKeyRecord>> ListAsync(Guid organisationId, CancellationToken ct);
    Task<ApiKeyRecord?> GetAsync(Guid organisationId, Guid keyId, CancellationToken ct);
    Task<ApiKeyRecord?> GetByPrefixAsync(string prefix, CancellationToken ct);
    Task<ApiKeyRecord?> RevokeAsync(Guid organisationId, Guid keyId, DateTime revokedUtc, CancellationToken ct);
}
