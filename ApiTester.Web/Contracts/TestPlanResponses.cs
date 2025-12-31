namespace ApiTester.Web.Contracts;

public sealed record TestPlanResponse(
    Guid ProjectId,
    string OperationId,
    string PlanJson,
    DateTime CreatedUtc);
