using ApiTester.McpServer.Models;

namespace ApiTester.Web.AI;

public sealed record RunExplanationContext(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    Guid? SpecId,
    Guid? BaselineRunId,
    string? EnvironmentName,
    string? EnvironmentBaseUrl,
    RunResultSummary Summary,
    IReadOnlyList<RunCaseSummary> Cases);

public sealed record RunResultSummary(
    int TotalCases,
    int Passed,
    int ExpectedBlocked,
    int Flaky,
    int RealFail,
    long TotalDurationMs,
    ResultClassificationSummary ClassificationSummary);

public sealed record RunCaseSummary(
    string Name,
    bool Pass,
    bool Blocked,
    int? StatusCode,
    long DurationMs,
    string? FailureReason,
    string? BlockReason,
    ResultClassification Classification);

public sealed record SpecSummaryContext(
    Guid SpecId,
    Guid ProjectId,
    string Title,
    string Version,
    DateTime CreatedUtc,
    string SpecJson);
