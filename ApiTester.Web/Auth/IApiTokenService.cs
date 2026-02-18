using ApiTester.McpServer.Models;

namespace ApiTester.Web.Auth;

public interface IApiTokenService
{
    Task<ApiTokenCreateResult> CreateTokenAsync(Guid tenantId, Guid userId, string name, IEnumerable<string> scopes, DateTime? expiresUtc, CancellationToken ct);
}

public sealed record ApiTokenCreateResult(ApiKeyRecord Record, string Token);
