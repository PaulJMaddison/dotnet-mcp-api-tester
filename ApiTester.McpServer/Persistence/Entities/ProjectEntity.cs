namespace ApiTester.McpServer.Persistence.Entities;

public sealed class ProjectEntity
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public List<TestRunEntity> Runs { get; set; } = new();   
    public string ProjectKey { get; set; } = "default";
}
