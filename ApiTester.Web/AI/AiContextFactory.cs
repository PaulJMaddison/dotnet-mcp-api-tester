using ApiTester.McpServer.Models;

namespace ApiTester.Web.AI;

public static class AiContextFactory
{
    public static RunExplanationContext BuildRunExplanationContext(TestRunRecord run)
    {
        var result = run.Result;
        var classification = result.ClassificationSummary;
        var summary = new RunResultSummary(
            result.TotalCases,
            classification.Pass,
            classification.BlockedExpected,
            classification.FlakyExternal,
            classification.Fail + classification.BlockedUnexpected,
            result.TotalDurationMs,
            classification);

        var cases = result.Results
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new RunCaseSummary(
                r.Name,
                r.Pass,
                r.Blocked,
                r.StatusCode,
                r.DurationMs,
                r.FailureReason,
                r.BlockReason,
                r.Classification))
            .ToList();

        return new RunExplanationContext(
            run.RunId,
            run.ProjectKey,
            run.OperationId,
            run.StartedUtc,
            run.CompletedUtc,
            run.SpecId,
            run.BaselineRunId,
            run.Environment?.Name,
            run.Environment?.BaseUrl,
            summary,
            cases);
    }

    public static SpecSummaryContext BuildSpecSummaryContext(OpenApiSpecRecord record)
        => new(
            record.SpecId,
            record.ProjectId,
            record.Title,
            record.Version,
            record.CreatedUtc,
            record.SpecJson);
}
