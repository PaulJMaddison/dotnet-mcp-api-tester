namespace ApiTester.Web.Contracts;

public sealed record AiRunExplanationResponse(Guid RunId, string Explanation);

public sealed record AiSpecSummaryResponse(Guid SpecId, string Summary);

public sealed record AiExplainExampleDto(string Title, string Content);

public sealed record AiRunSummaryEvidenceRefDto(string CaseName, string? FailureReason);

public sealed record AiRunSummaryFailureDto(string Title, IReadOnlyList<AiRunSummaryEvidenceRefDto> EvidenceRefs);

public sealed record AiRunSummaryRegressionLikelihoodDto(string Level, string Rationale);

public sealed record AiRunSummaryResponse(
    Guid RunId,
    string OverallSummary,
    IReadOnlyList<AiRunSummaryFailureDto> TopFailures,
    string FlakeAssessment,
    AiRunSummaryRegressionLikelihoodDto RegressionLikelihood,
    IReadOnlyList<string> RecommendedNextActions);

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

public sealed record AiImprovementSuggestionDto(string Type, string PayloadJson);

public sealed record AiSuggestImprovementsResponse(
    Guid RunId,
    IReadOnlyList<AiImprovementSuggestionDto> Suggestions);
