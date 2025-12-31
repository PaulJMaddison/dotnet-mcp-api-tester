namespace ApiTester.McpServer.Persistence.Entities;

public sealed class TestPlanEntity
{
    public Guid ProjectId { get; set; }
    public string OperationId { get; set; } = "";
    public string PlanJson { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public ProjectEntity Project { get; set; } = null!;
}
