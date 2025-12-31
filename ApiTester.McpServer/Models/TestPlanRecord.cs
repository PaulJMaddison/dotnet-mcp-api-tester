namespace ApiTester.McpServer.Models;

public sealed record TestPlanRecord(
    Guid ProjectId,
    string OperationId,
    string PlanJson,
    DateTime CreatedUtc);
