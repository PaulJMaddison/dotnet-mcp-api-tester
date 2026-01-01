namespace ApiTester.McpServer.Models;

public sealed record RunAnnotationRecord(
    Guid AnnotationId,
    Guid RunId,
    string OwnerKey,
    string Note,
    string? JiraLink,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
