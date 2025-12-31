using ApiTester.McpServer.Models;

namespace ApiTester.Web.Contracts;

public sealed record RunSummaryResponse(string ProjectKey, int Take, int Total, IReadOnlyList<RunSummaryItem> Runs);

public sealed record RunSummaryItem(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    RunSummary Summary);

public sealed record RunSummary(
    int TotalCases,
    int Passed,
    int Failed,
    int Blocked,
    long TotalDurationMs);

public sealed record RunDetailResponse(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    TestRunResult Result);
