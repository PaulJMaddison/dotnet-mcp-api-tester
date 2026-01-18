using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class OrgMapping
{
    public static OrgDto ToDto(OrganisationRecord record)
        => new(
            record.OrganisationId,
            record.Name,
            record.Slug,
            record.CreatedUtc,
            record.RetentionDays,
            record.RedactionRules);

    public static OrgMemberDto ToMemberDto(UserRecord user, OrgRole role)
        => new(user.UserId, user.DisplayName, user.Email, role);
}
