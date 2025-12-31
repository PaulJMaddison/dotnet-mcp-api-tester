namespace ApiTester.McpServer.Persistence.Entities;

public sealed class ProjectEntity
{
    public Guid ProjectId { get; set; }
    public string OwnerKey { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public List<TestRunEntity> Runs { get; set; } = new();
    public List<TestPlanEntity> TestPlans { get; set; } = new();
    public OpenApiSpecEntity? OpenApiSpec { get; set; }
    public string ProjectKey { get; set; } = "";
}
