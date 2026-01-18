namespace ApiTester.McpServer.Models;

public sealed record ApiKeyRecord(
    Guid KeyId,
    Guid OrganisationId,
    Guid UserId,
    string Name,
    string Scopes,
    DateTime? ExpiresUtc,
    DateTime? RevokedUtc,
    string Hash,
    string Prefix);
