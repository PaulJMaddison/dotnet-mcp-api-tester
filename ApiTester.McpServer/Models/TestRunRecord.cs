namespace ApiTester.McpServer.Models;

public sealed class TestRunRecord
{
    public Guid RunId { get; set; }
    public Guid OrganisationId { get; set; } = OrgDefaults.DefaultOrganisationId;
    public Guid TenantId { get; set; } = OrgDefaults.DefaultOrganisationId;
    public string Actor { get; init; } = OwnerKeyDefaults.Default;
    public TestRunEnvironmentSnapshot? Environment { get; init; }
    public ApiExecutionPolicySnapshot? PolicySnapshot { get; init; }
    public string OwnerKey { get; init; } = OwnerKeyDefaults.Default;
    public string OperationId { get; set; } = "";
    public Guid? SpecId { get; set; }
    public Guid? BaselineRunId { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }

    public required TestRunResult Result { get; init; }
    public string ProjectKey { get; init; } = "default";
}
