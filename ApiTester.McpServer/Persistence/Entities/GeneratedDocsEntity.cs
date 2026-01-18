namespace ApiTester.McpServer.Persistence.Entities;

public sealed class GeneratedDocsEntity
{
    public Guid DocsId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SpecId { get; set; }
    public string DocsJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public ProjectEntity? Project { get; set; }
    public OrganisationEntity? Organisation { get; set; }
}
