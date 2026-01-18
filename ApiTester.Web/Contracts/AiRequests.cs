namespace ApiTester.Web.Contracts;

public sealed record AiExplainRequest(string? ProjectId, string? OperationId);

public sealed record AiSuggestTestsRequest(string? ProjectId, string? OperationId);

public sealed record AiSummariseRunRequest(string? RunId);

public sealed record AiGenerateDocsRequest(string? ProjectId);

public sealed record AiComplianceReportRequest(string? RunId);
