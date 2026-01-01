namespace ApiTester.McpServer.Persistence.Entities;

public sealed class EnvironmentEntity
{
    public Guid EnvironmentId { get; set; }
    public Guid ProjectId { get; set; }
    public string OwnerKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public ProjectEntity? Project { get; set; }
}
