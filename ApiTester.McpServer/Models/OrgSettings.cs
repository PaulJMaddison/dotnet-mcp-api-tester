namespace ApiTester.McpServer.Models;

public enum OrgPlan
{
    Free,
    Pro,
    Team
}

public sealed record OrgSettings(OrgPlan Plan)
{
    public static OrgSettings Default => new(OrgPlan.Free);

    public bool IsAiEnabled => Plan is OrgPlan.Pro or OrgPlan.Team;
}
