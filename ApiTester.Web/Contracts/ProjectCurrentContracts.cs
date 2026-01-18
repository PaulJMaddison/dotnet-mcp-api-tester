namespace ApiTester.Web.Contracts;

public sealed record ProjectCurrentRequest(Guid ProjectId);

public sealed record ProjectCurrentResponse(Guid? CurrentProjectId);
