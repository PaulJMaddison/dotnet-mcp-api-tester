namespace ApiTester.Web.Contracts;

public sealed record AiRunExplanationResponse(Guid RunId, string Explanation);

public sealed record AiSpecSummaryResponse(Guid SpecId, string Summary);
