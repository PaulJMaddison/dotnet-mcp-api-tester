using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class RunMapping
{
    public static RunSummaryResponse ToSummaryResponse(string projectKey, int take, IReadOnlyList<TestRunRecord> runs)
    {
        var items = runs.Select(ToSummaryItem).ToList();
        return new RunSummaryResponse(projectKey, take, items.Count, items);
    }

    public static RunSummaryItem ToSummaryItem(TestRunRecord record)
    {
        var summary = new RunSummary(
            record.Result.TotalCases,
            record.Result.Passed,
            record.Result.Failed,
            record.Result.Blocked,
            record.Result.TotalDurationMs);

        return new RunSummaryItem(
            record.RunId,
            record.ProjectKey,
            record.OperationId,
            record.StartedUtc,
            record.CompletedUtc,
            summary);
    }

    public static RunDetailResponse ToDetailResponse(TestRunRecord record)
        => new(
            record.RunId,
            record.ProjectKey,
            record.OperationId,
            record.StartedUtc,
            record.CompletedUtc,
            record.Result);
}
