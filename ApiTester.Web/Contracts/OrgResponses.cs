using ApiTester.McpServer.Models;

namespace ApiTester.Web.Contracts;

public sealed record OrgDto(Guid OrganisationId, string Name, string Slug, DateTime CreatedUtc);

public sealed record OrgMemberDto(Guid UserId, string DisplayName, string? Email, OrgRole Role);

public sealed record OrgMembersResponse(IReadOnlyList<OrgMemberDto> Members);
