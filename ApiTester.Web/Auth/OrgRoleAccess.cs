using ApiTester.McpServer.Models;

namespace ApiTester.Web.Auth;

public static class OrgRoleAccess
{
    public static bool CanViewMembers(OrgRole role) => role >= OrgRole.Member;
    public static bool CanManageMembers(OrgRole role) => role >= OrgRole.Admin;
}
