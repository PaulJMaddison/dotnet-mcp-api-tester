namespace ApiTester.McpServer.Persistence.Entities;

public sealed class OpenApiSpecEntity
{
    public Guid SpecId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public string SpecJson { get; set; } = "";
    public string SpecHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public ProjectEntity Project { get; set; } = null!;
}
