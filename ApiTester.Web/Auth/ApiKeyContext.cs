namespace ApiTester.Web.Auth;

public sealed record ApiKeyContext(
    Guid KeyId,
    Guid OrganisationId,
    Guid UserId,
    IReadOnlySet<string> Scopes,
    DateTime? ExpiresUtc,
    DateTime? RevokedUtc,
    string Prefix);
