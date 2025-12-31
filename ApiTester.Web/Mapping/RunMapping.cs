using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class RunMapping
{
    public static RunSummaryResponse ToSummaryResponse(string projectKey, PageMetadata metadata, IReadOnlyList<TestRunRecord> runs)
    {
        var items = runs.Select(ToSummaryDto).ToList();
        return new RunSummaryResponse(projectKey, items, metadata);
    }

    public static RunSummaryDto ToSummaryDto(TestRunRecord record)
    {
        var summary = new RunSummary(
            record.Result.TotalCases,
            record.Result.Passed,
            record.Result.Failed,
            record.Result.Blocked,
            record.Result.TotalDurationMs);

        return new RunSummaryDto(
            record.RunId,
            record.ProjectKey,
            record.OperationId,
            record.StartedUtc,
            record.CompletedUtc,
            summary);
    }

    public static RunDetailDto ToDetailDto(TestRunRecord record)
        => new(
            record.RunId,
            record.ProjectKey,
            record.OperationId,
            record.StartedUtc,
            record.CompletedUtc,
            record.Result);
}
