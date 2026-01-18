namespace ApiTester.McpServer.Models;

public sealed record TestPlanDraftRecord(
    Guid DraftId,
    Guid ProjectId,
    string OperationId,
    string PlanJson,
    DateTime CreatedUtc);
