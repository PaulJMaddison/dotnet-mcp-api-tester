namespace ApiTester.McpServer.Persistence.Entities;

public sealed class AuditEventEntity
{
    public Guid AuditEventId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string? MetadataJson { get; set; }

    public OrganisationEntity? Organisation { get; set; }
}
