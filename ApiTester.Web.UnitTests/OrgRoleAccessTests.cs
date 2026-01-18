using ApiTester.McpServer.Models;
using ApiTester.Web.Auth;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class OrgRoleAccessTests
{
    [Theory]
    [InlineData(OrgRole.Viewer, false)]
    [InlineData(OrgRole.Member, true)]
    [InlineData(OrgRole.Admin, true)]
    [InlineData(OrgRole.Owner, true)]
    public void CanViewMembers_RequiresMemberOrAbove(OrgRole role, bool expected)
    {
        Assert.Equal(expected, OrgRoleAccess.CanViewMembers(role));
    }

    [Theory]
    [InlineData(OrgRole.Viewer, false)]
    [InlineData(OrgRole.Member, false)]
    [InlineData(OrgRole.Admin, true)]
    [InlineData(OrgRole.Owner, true)]
    public void CanManageMembers_RequiresAdminOrAbove(OrgRole role, bool expected)
    {
        Assert.Equal(expected, OrgRoleAccess.CanManageMembers(role));
    }
}
