using ApiTester.McpServer.Models;

namespace ApiTester.Web.Auth;

public sealed record OrgContext(
    Guid OrganisationId,
    Guid UserId,
    OrgRole Role,
    string OwnerKey,
    bool IsLocalDev);
