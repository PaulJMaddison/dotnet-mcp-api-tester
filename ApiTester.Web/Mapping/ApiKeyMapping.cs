using ApiTester.McpServer.Models;
using ApiTester.Web.Auth;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class ApiKeyMapping
{
    public static ApiKeyDto ToDto(ApiKeyRecord record)
    {
        var scopes = ApiKeyScopes.Parse(record.Scopes).OrderBy(scope => scope).ToList();
        return new ApiKeyDto(record.KeyId, record.UserId, record.Name, scopes, record.ExpiresUtc, record.RevokedUtc, record.Prefix);
    }

    public static ApiKeyListResponse ToListResponse(IEnumerable<ApiKeyRecord> records)
        => new(records.Select(ToDto).ToList());
}
