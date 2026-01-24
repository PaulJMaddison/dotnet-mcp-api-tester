namespace ApiTester.Web.Contracts;

public sealed record AdminTenantCreateRequest(
    string Name,
    string Slug,
    string OwnerExternalId,
    string? OwnerDisplayName,
    string? OwnerEmail);

public sealed record AdminApiKeyCreateRequest(
    Guid OrganisationId,
    Guid UserId,
    string Name,
    List<string> Scopes,
    DateTime? ExpiresUtc);
