namespace ApiTester.Web.Contracts;

public sealed record ApiKeyDto(
    Guid KeyId,
    Guid UserId,
    string Name,
    IReadOnlyList<string> Scopes,
    DateTime? ExpiresUtc,
    DateTime? RevokedUtc,
    string Prefix);

public sealed record ApiKeyListResponse(IReadOnlyList<ApiKeyDto> Keys);

public sealed record ApiKeyCreateResponse(ApiKeyDto ApiKey, string Key);
