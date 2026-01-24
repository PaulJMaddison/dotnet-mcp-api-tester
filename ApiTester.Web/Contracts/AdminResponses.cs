namespace ApiTester.Web.Contracts;

public sealed record AdminUserDto(
    Guid UserId,
    string ExternalId,
    string DisplayName,
    string? Email);

public sealed record AdminTenantCreateResponse(
    OrgDto Organisation,
    AdminUserDto Owner);
