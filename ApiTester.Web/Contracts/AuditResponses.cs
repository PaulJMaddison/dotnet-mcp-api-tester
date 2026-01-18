namespace ApiTester.Web.Contracts;

public sealed record AuditEventResponse(
    Guid OrgId,
    Guid ActorUserId,
    string Action,
    string TargetType,
    string TargetId,
    DateTime CreatedUtc,
    string? MetadataJson);

public sealed record AuditListResponse(IReadOnlyList<AuditEventResponse> Events);
