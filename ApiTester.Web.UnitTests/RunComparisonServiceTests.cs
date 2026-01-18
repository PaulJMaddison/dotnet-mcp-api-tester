using ApiTester.McpServer.Models;
using ApiTester.Web.Comparison;
using ApiTester.Web.Contracts;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class RunComparisonServiceTests
{
    private readonly RunComparisonService _service = new();

    [Fact]
    public void Compare_MissingCaseInBaseline_IsReported()
    {
        var baseline = BuildRun(
            CreateCase("case-a", pass: true));

        var run = BuildRun(
            CreateCase("case-a", pass: true),
            CreateCase("case-b", pass: false));

        var result = _service.Compare(run, baseline);

        Assert.Single(result.MissingInBaseline);
        Assert.Equal("case-b", result.MissingInBaseline[0].Name);
    }

    [Fact]
    public void Compare_MissingCaseInRun_IsReported()
    {
        var baseline = BuildRun(
            CreateCase("case-a", pass: true),
            CreateCase("case-b", pass: false));

        var run = BuildRun(
            CreateCase("case-a", pass: true));

        var result = _service.Compare(run, baseline);

        Assert.Single(result.MissingInRun);
        Assert.Equal("case-b", result.MissingInRun[0].Name);
    }

    [Fact]
    public void Compare_RenamedCase_IsReported()
    {
        var baseline = BuildRun(
            CreateCase("case-a", pass: true, method: "GET", url: "https://example.test/a"));

        var run = BuildRun(
            CreateCase("case-a-renamed", pass: true, method: "GET", url: "https://example.test/a"));

        var result = _service.Compare(run, baseline);

        Assert.Single(result.RenamedCases);
        Assert.Equal("case-a", result.RenamedCases[0].BaselineName);
        Assert.Equal("case-a-renamed", result.RenamedCases[0].RunName);
    }

    [Fact]
    public void Compare_NullStatusCodes_DoNotBreakComparison()
    {
        var baseline = BuildRun(
            CreateCase("case-a", pass: true, statusCode: null));

        var run = BuildRun(
            CreateCase("case-a", pass: false, statusCode: null));

        var result = _service.Compare(run, baseline);

        Assert.Single(result.PassToFail);
        Assert.Null(result.PassToFail[0].BaselineStatusCode);
        Assert.Null(result.PassToFail[0].RunStatusCode);
    }

    [Fact]
    public void Compare_BlockedToFail_IsReported()
    {
        var baseline = BuildRun(
            CreateCase("case-a", pass: false, blocked: true));

        var run = BuildRun(
            CreateCase("case-a", pass: false, blocked: false));

        var result = _service.Compare(run, baseline);

        Assert.Single(result.BlockedChanges);
        Assert.Equal(TestCaseOutcome.Blocked, result.BlockedChanges[0].BaselineOutcome);
        Assert.Equal(TestCaseOutcome.Failed, result.BlockedChanges[0].RunOutcome);
    }

    [Fact]
    public void Compare_StatusCodeChange_IsReported()
    {
        var baseline = BuildRun(
            CreateCase("case-a", pass: true, statusCode: 200));

        var run = BuildRun(
            CreateCase("case-a", pass: true, statusCode: 500));

        var result = _service.Compare(run, baseline);

        Assert.Single(result.StatusChanges);
        Assert.Equal(200, result.StatusChanges[0].BaselineStatusCode);
        Assert.Equal(500, result.StatusChanges[0].RunStatusCode);
    }

    [Fact]
    public void Compare_ResponseSnippetChange_IsReported()
    {
        var baseline = BuildRun(
            CreateCase("case-a", pass: true, responseSnippet: "{\"ok\":true}"));

        var run = BuildRun(
            CreateCase("case-a", pass: true, responseSnippet: "{\"ok\":false}"));

        var result = _service.Compare(run, baseline);

        Assert.Single(result.ResponseSnippetChanges);
        Assert.NotNull(result.ResponseSnippetChanges[0].BaselineSnippetHash);
        Assert.NotNull(result.ResponseSnippetChanges[0].RunSnippetHash);
    }

    private static TestRunRecord BuildRun(params TestCaseResult[] results)
    {
        var total = results.Length;
        var passed = results.Count(r => r.Pass && !r.Blocked);
        var failed = results.Count(r => !r.Pass && !r.Blocked);
        var blocked = results.Count(r => r.Blocked);
        var duration = results.Sum(r => r.DurationMs);

        return new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            ProjectKey = "project",
            OperationId = "op-1",
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = "op-1",
                TotalCases = total,
                Passed = passed,
                Failed = failed,
                Blocked = blocked,
                TotalDurationMs = duration,
                Results = results.ToList()
            }
        };
    }

    private static TestCaseResult CreateCase(
        string name,
        bool pass,
        bool blocked = false,
        int? statusCode = 200,
        long durationMs = 100,
        string? method = "GET",
        string? url = "https://example.test",
        string? responseSnippet = null)
    {
        return new TestCaseResult
        {
            Name = name,
            Method = method,
            Url = url,
            StatusCode = statusCode,
            DurationMs = durationMs,
            Pass = pass,
            Blocked = blocked,
            ResponseSnippet = responseSnippet
        };
    }
}
