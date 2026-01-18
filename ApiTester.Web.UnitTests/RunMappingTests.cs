using ApiTester.McpServer.Models;
using ApiTester.Web.Mapping;
using Xunit;

namespace ApiTester.Web.UnitTests;

public class RunMappingTests
{
    [Fact]
    public void ToSummaryResponse_MapsRunCounts()
    {
        var record = BuildRunRecord();

        var metadata = new ApiTester.Web.Contracts.PageMetadata(10, 20, "next");
        var response = RunMapping.ToSummaryResponse(record.ProjectKey, metadata, new[] { record });

        Assert.Equal(record.ProjectKey, response.ProjectKey);
        Assert.Equal(metadata, response.Metadata);
        Assert.Single(response.Runs);
        Assert.Equal(record.RunId, response.Runs[0].RunId);
        Assert.Equal(record.Result.TotalCases, response.Runs[0].Snapshot.TotalCases);
        Assert.Equal(record.Result.ClassificationSummary.Pass, response.Runs[0].Snapshot.Passed);
    }

    [Fact]
    public void ToDetailResponse_MapsResultPayload()
    {
        var record = BuildRunRecord();

        var response = RunMapping.ToDetailDto(record);

        Assert.Equal(record.RunId, response.RunId);
        Assert.Equal(record.Result.Results.Count, response.Result.Results.Count);
        Assert.Equal(record.Result.Results[0].Name, response.Result.Results[0].Name);
    }

    [Fact]
    public void ToComplianceReport_MapsPolicyAuditAndLimits()
    {
        var record = BuildRunRecord();

        var response = RunMapping.ToComplianceReport(record);

        Assert.Equal(record.RunId, response.RunId);
        Assert.Equal(record.Actor, response.Audit.Actor);
        Assert.Equal(record.ProjectKey, response.Audit.ProjectKey);
        Assert.Equal(record.OperationId, response.Audit.OperationId);
        Assert.Equal(record.Environment?.Name, response.Audit.Environment?.Name);
        Assert.Equal(record.PolicySnapshot?.DryRun, response.Policy?.DryRun);
        Assert.Equal(record.PolicySnapshot?.AllowedMethods, response.Policy?.AllowedMethods);
        Assert.Equal(record.PolicySnapshot?.AllowedBaseUrls, response.Ssrf?.AllowedBaseUrls);
        Assert.Equal(record.PolicySnapshot?.BlockLocalhost, response.Ssrf?.BlockLocalhost);
        Assert.Equal(record.PolicySnapshot?.TimeoutSeconds, response.Limits?.TimeoutSeconds);
        Assert.Equal(record.PolicySnapshot?.MaxRequestBodyBytes, response.Limits?.MaxRequestBodyBytes);
        Assert.Equal(record.PolicySnapshot?.MaxResponseBodyBytes, response.Limits?.MaxResponseBodyBytes);
        Assert.Equal(record.PolicySnapshot?.RetryOnFlake, response.Policy?.RetryOnFlake);
        Assert.Equal(record.PolicySnapshot?.MaxRetries, response.Policy?.MaxRetries);
    }

    private static TestRunRecord BuildRunRecord()
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
                Url = "https://example.test",
                StatusCode = 500,
                DurationMs = 200,
                Pass = false,
                FailureReason = "Error"
            }
        };

        var summary = ResultClassificationRules.Summarize(results);

        return new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            Actor = "tester@example.com",
            Environment = new TestRunEnvironmentSnapshot("Staging", "https://staging.example.test"),
            PolicySnapshot = new ApiExecutionPolicySnapshot(
                false,
                new[] { "https://api.example.test" },
                new[] { "GET", "POST" },
                true,
                true,
                30,
                1024,
                2048,
                true,
                2),
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
