namespace ApiTester.McpServer.Persistence.Entities;

public sealed class RunAnnotationEntity
{
    public Guid AnnotationId { get; set; }
    public Guid RunId { get; set; }
    public TestRunEntity? Run { get; set; }
    public string OwnerKey { get; set; } = "";
    public string Note { get; set; } = "";
    public string? JiraLink { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
