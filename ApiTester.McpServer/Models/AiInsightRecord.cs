namespace ApiTester.McpServer.Models;

public sealed record AiInsightRecord(
    Guid InsightId,
    Guid OrganisationId,
    Guid ProjectId,
    Guid RunId,
    string OperationId,
    string Type,
    string JsonPayload,
    string ModelId,
    DateTime CreatedUtc);

public sealed record AiInsightCreate(
    string Type,
    string JsonPayload);
