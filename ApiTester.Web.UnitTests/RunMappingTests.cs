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

        var response = RunMapping.ToSummaryResponse(record.ProjectKey, 20, new[] { record });

        Assert.Equal(record.ProjectKey, response.ProjectKey);
        Assert.Equal(20, response.Take);
        Assert.Single(response.Runs);
        Assert.Equal(record.RunId, response.Runs[0].RunId);
        Assert.Equal(record.Result.TotalCases, response.Runs[0].Summary.TotalCases);
    }

    [Fact]
    public void ToDetailResponse_MapsResultPayload()
    {
        var record = BuildRunRecord();

        var response = RunMapping.ToDetailResponse(record);

        Assert.Equal(record.RunId, response.RunId);
        Assert.Equal(record.Result.Results.Count, response.Result.Results.Count);
        Assert.Equal(record.Result.Results[0].Name, response.Result.Results[0].Name);
    }

    private static TestRunRecord BuildRunRecord()
    {
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
                Results =
                [
                    new TestCaseResult
                    {
                        Name = "case-1",
                        Method = "GET",
                        Url = "https://example.test",
                        StatusCode = 200,
                        DurationMs = 100,
                        Pass = true
                    },
                    new TestCaseResult
                    {
                        Name = "case-2",
                        Method = "POST",
                        Url = "https://example.test",
                        StatusCode = 500,
                        DurationMs = 200,
                        Pass = false,
                        FailureReason = "Error"
                    }
                ]
            }
        };
    }
}
