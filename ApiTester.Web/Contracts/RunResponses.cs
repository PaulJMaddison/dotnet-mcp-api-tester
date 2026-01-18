using ApiTester.McpServer.Models;

namespace ApiTester.Web.Contracts;

public sealed record RunSummaryResponse(string ProjectKey, IReadOnlyList<RunSummaryDto> Runs, PageMetadata Metadata);

public sealed record RunSummaryDto(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    RunSummary Snapshot);

public sealed record RunSummary(
    int TotalCases,
    int Passed,
    int ExpectedBlocked,
    int Flaky,
    int RealFail,
    long TotalDurationMs,
    ResultClassificationSummary ClassificationSummary);

public sealed record RunDetailDto(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    TestRunResult Result);

public sealed record RunSummaryCountsResponse(
    Guid RunId,
    int TotalCases,
    int Passed,
    int ExpectedBlocked,
    int Flaky,
    int RealFail);
