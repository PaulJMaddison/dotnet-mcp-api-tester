using ApiTester.McpServer.Models;

namespace ApiTester.Web.Contracts;

public enum TestCaseOutcome
{
    Passed,
    Failed,
    Blocked
}

public sealed record TestCaseComparisonDto(
    string Name,
    string? Method,
    string? Url,
    TestCaseOutcome BaselineOutcome,
    TestCaseOutcome RunOutcome,
    int? BaselineStatusCode,
    int? RunStatusCode,
    long BaselineDurationMs,
    long RunDurationMs);

public sealed record TestCaseDurationDeltaDto(
    string Name,
    string? Method,
    string? Url,
    long BaselineDurationMs,
    long RunDurationMs,
    long DeltaMs);

public sealed record TestCaseRenameDto(
    string BaselineName,
    string RunName,
    string? Method,
    string? Url);

public sealed record RunComparisonResponse(
    Guid RunId,
    Guid BaselineRunId,
    IReadOnlyList<TestCaseComparisonDto> NewFailures,
    IReadOnlyList<TestCaseComparisonDto> FixedFailures,
    IReadOnlyList<TestCaseComparisonDto> PassToFail,
    IReadOnlyList<TestCaseComparisonDto> FailToPass,
    IReadOnlyList<TestCaseComparisonDto> BlockedChanges,
    IReadOnlyList<TestCaseDurationDeltaDto> DurationDeltas,
    IReadOnlyList<TestCaseResult> MissingInBaseline,
    IReadOnlyList<TestCaseResult> MissingInRun,
    IReadOnlyList<TestCaseRenameDto> RenamedCases);
