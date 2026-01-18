namespace ApiTester.McpServer.Persistence.Entities;

public sealed class BaselineRunEntity
{
    public Guid ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }
    public string OperationId { get; set; } = "";
    public Guid RunId { get; set; }
    public TestRunEntity? Run { get; set; }
    public DateTime SetUtc { get; set; }
}
