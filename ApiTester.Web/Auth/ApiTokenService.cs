using ApiTester.McpServer.Persistence.Stores;

namespace ApiTester.Web.Auth;

public sealed class ApiTokenService : IApiTokenService
{
    private readonly IApiKeyStore _apiKeyStore;

    public ApiTokenService(IApiKeyStore apiKeyStore)
    {
        _apiKeyStore = apiKeyStore;
    }

    public async Task<ApiTokenCreateResult> CreateTokenAsync(Guid tenantId, Guid userId, string name, IEnumerable<string> scopes, DateTime? expiresUtc, CancellationToken ct)
    {
        var token = ApiKeyToken.Generate();
        var serializedScopes = ApiKeyScopes.Serialize(scopes);
        var hash = ApiKeyHasher.Hash(token.Token);

        var record = await _apiKeyStore.CreateAsync(
            tenantId,
            userId,
            name,
            serializedScopes,
            expiresUtc,
            hash,
            token.Prefix,
            ct);

        return new ApiTokenCreateResult(record, token.Token);
    }
}
