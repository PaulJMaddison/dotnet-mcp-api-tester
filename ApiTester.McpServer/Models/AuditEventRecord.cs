namespace ApiTester.McpServer.Models;

public sealed record AuditEventRecord(
    Guid AuditEventId,
    Guid OrganisationId,
    Guid ActorUserId,
    string Action,
    string TargetType,
    string TargetId,
    DateTime CreatedUtc,
    string? MetadataJson);
