namespace ApiTester.Web.Contracts;

public sealed record AiRunExplanationResponse(Guid RunId, string Explanation);

public sealed record AiSpecSummaryResponse(Guid SpecId, string Summary);

public sealed record AiExplainExampleDto(string Title, string Content);

public sealed record AiExplainResponse(
    Guid ProjectId,
    string OperationId,
    string Summary,
    string Inputs,
    string Outputs,
    string Auth,
    IReadOnlyList<string> Gotchas,
    IReadOnlyList<AiExplainExampleDto> Examples,
    string Markdown);

public sealed record AiSuggestTestsResponse(
    Guid DraftId,
    Guid ProjectId,
    string OperationId,
    string PlanJson,
    DateTime CreatedUtc);
