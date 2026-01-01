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
