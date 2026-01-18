namespace ApiTester.Site.Models;

public sealed record PageMetadata(int Total, int PageSize, string? NextPageToken);

public sealed record ProjectDto(Guid ProjectId, string Name, string ProjectKey, DateTime CreatedUtc);

public sealed record ProjectListResponse(IReadOnlyList<ProjectDto> Projects, PageMetadata Metadata);

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

public sealed class TestRunResult
{
    public required string OperationId { get; init; }
    public required int TotalCases { get; init; }
    public required int Passed { get; init; }
    public required int Failed { get; init; }
    public required int Blocked { get; init; }
    public required long TotalDurationMs { get; init; }

    public ResultClassificationSummary ClassificationSummary { get; init; } = new();

    public List<TestCaseResult> Results { get; init; } = new();
}

public sealed class TestCaseResult
{
    public required string Name { get; init; }

    public bool Blocked { get; init; }
    public string? BlockReason { get; init; }

    public string? Url { get; init; }
    public string? Method { get; init; }

    public int? StatusCode { get; init; }
    public long DurationMs { get; init; }

    public bool Pass { get; init; }
    public string? FailureReason { get; init; }

    public string? ResponseSnippet { get; init; }

    public bool IsFlaky { get; init; }
    public string? FlakeReasonCategory { get; init; }

    public ResultClassification Classification { get; set; }
}

public enum ResultClassification
{
    Pass,
    Fail,
    BlockedExpected,
    BlockedUnexpected,
    FlakyExternal
}

public sealed class ResultClassificationSummary
{
    public int Pass { get; set; }
    public int Fail { get; set; }
    public int BlockedExpected { get; set; }
    public int BlockedUnexpected { get; set; }
    public int FlakyExternal { get; set; }
}
