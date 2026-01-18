using System.Text.Json;
using System.Xml.Linq;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Services;
using ApiTester.Web.Reports;

namespace ApiTester.Web.UnitTests;

public class RunExportGeneratorTests
{
    [Fact]
    public void GenerateJunit_ProducesSuiteAndCaseNodes()
    {
        var run = BuildRunRecord();

        var junit = RunExportGenerator.GenerateJunit(run);

        var doc = XDocument.Parse(junit);
        var suite = doc.Root;
        Assert.NotNull(suite);
        Assert.Equal("testsuite", suite!.Name.LocalName);
        Assert.Equal("3", suite.Attribute("tests")?.Value);
        Assert.Equal("1", suite.Attribute("failures")?.Value);
        Assert.Equal("1", suite.Attribute("skipped")?.Value);
        Assert.Equal(3, suite.Elements("testcase").Count());
    }

    [Fact]
    public void Exports_AreRedacted()
    {
        var run = BuildRunRecord("super-secret");
        var redactionService = new RedactionService();
        var redacted = RunExportRedactor.RedactRun(run, redactionService, new[] { "super-secret" });

        var json = RunExportGenerator.GenerateJson(redacted, new JsonSerializerOptions { WriteIndented = false });
        var csv = RunExportGenerator.GenerateCsv(redacted);
        var junit = RunExportGenerator.GenerateJunit(redacted);

        Assert.DoesNotContain("super-secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret", junit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", json, StringComparison.OrdinalIgnoreCase);
    }

    private static TestRunRecord BuildRunRecord(string? secret = null)
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
                Url = "https://example.test/blocked",
                StatusCode = 0,
                DurationMs = 50,
                Blocked = true,
                BlockReason = "Missing required header"
            },
            new()
            {
                Name = "case-3",
                Method = "POST",
                Url = "https://example.test/error",
                StatusCode = 500,
                DurationMs = 200,
                Pass = false,
                FailureReason = "Failure",
                ResponseSnippet = secret ?? "error"
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
                TotalCases = 3,
                Passed = 1,
                Failed = 1,
                Blocked = 1,
                TotalDurationMs = 350,
                ClassificationSummary = summary,
                Results = results
            }
        };
    }
}
