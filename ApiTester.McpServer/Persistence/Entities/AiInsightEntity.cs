namespace ApiTester.McpServer.Persistence.Entities;

public sealed class AiInsightEntity
{
    public Guid InsightId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid RunId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
