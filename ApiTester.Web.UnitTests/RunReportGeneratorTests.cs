using ApiTester.McpServer.Models;
using ApiTester.Web.Reports;

namespace ApiTester.Web.UnitTests;

public class RunReportGeneratorTests
{
    [Fact]
    public void GenerateMarkdown_IncludesSummaryAndCases()
    {
        var run = BuildRunRecord();

        var report = RunReportGenerator.Generate(run, RunReportFormat.Markdown);

        Assert.Contains("# Test Run Report", report);
        Assert.Contains("| Total Cases | 2 |", report);
        Assert.Contains("| case-1 | Passed", report);
        Assert.Contains("| case-2 | Failed", report);
    }

    [Fact]
    public void GenerateHtml_IncludesSummaryAndCases()
    {
        var run = BuildRunRecord();

        var report = RunReportGenerator.Generate(run, RunReportFormat.Html);

        Assert.Contains("<h1>Test Run Report</h1>", report);
        Assert.Contains("<td>Total Cases</td><td>2</td>", report);
        Assert.Contains("<td>case-1</td>", report);
        Assert.Contains("<td>case-2</td>", report);
    }

    [Fact]
    public void Generate_TrimsLargePayloads()
    {
        var longPayload = new string('x', 2100);
        var run = BuildRunRecord(longPayload);

        var markdownReport = RunReportGenerator.Generate(run, RunReportFormat.Markdown);
        var htmlReport = RunReportGenerator.Generate(run, RunReportFormat.Html);

        Assert.Contains("… (truncated)", markdownReport);
        Assert.Contains("… (truncated)", htmlReport);
        Assert.DoesNotContain(longPayload, markdownReport);
        Assert.DoesNotContain(longPayload, htmlReport);
    }

    private static TestRunRecord BuildRunRecord(string? longPayload = null)
    {
        var results = new List<TestCaseResult>
        {
            new()
            {
                Name = "case-1",
                Method = "GET",
                Url = "https://example.test",
                StatusCode = 200,
                DurationMs = 100,
                Pass = true
            },
            new()
            {
                Name = "case-2",
                Method = "POST",
                Url = "https://example.test/" + (longPayload ?? "error"),
                StatusCode = 500,
                DurationMs = 200,
                Pass = false,
                FailureReason = longPayload ?? "Error",
                ResponseSnippet = longPayload
            }
        };

        var summary = ResultClassificationRules.Summarize(results);

        return new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            ProjectKey = "sample-project",
            OperationId = "op-1",
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = "op-1",
                TotalCases = 2,
                Passed = 1,
                Failed = 1,
                Blocked = 0,
                TotalDurationMs = 1234,
                ClassificationSummary = summary,
                Results = results
            }
        };
    }
}
