using System;

namespace ApiTester.McpServer.Models;

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

public static class ResultClassificationRules
{
    public static ResultClassification Classify(TestCaseResult result)
    {
        if (result.Blocked)
            return IsExpectedBlocked(result.BlockReason) ? ResultClassification.BlockedExpected : ResultClassification.BlockedUnexpected;

        if (result.Pass)
            return ResultClassification.Pass;

        if (IsFlakyExternal(result.StatusCode, result.ResponseSnippet, result.FailureReason))
            return ResultClassification.FlakyExternal;

        return ResultClassification.Fail;
    }

    public static ResultClassificationSummary Summarize(IReadOnlyCollection<TestCaseResult> results)
    {
        var summary = new ResultClassificationSummary();

        foreach (var result in results)
        {
            var classification = Classify(result);
            result.Classification = classification;

            switch (classification)
            {
                case ResultClassification.Pass:
                    summary.Pass++;
                    break;
                case ResultClassification.Fail:
                    summary.Fail++;
                    break;
                case ResultClassification.BlockedExpected:
                    summary.BlockedExpected++;
                    break;
                case ResultClassification.BlockedUnexpected:
                    summary.BlockedUnexpected++;
                    break;
                case ResultClassification.FlakyExternal:
                    summary.FlakyExternal++;
                    break;
            }
        }

        return summary;
    }

    private static bool IsExpectedBlocked(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return false;

        return reason.StartsWith("Missing required path param", StringComparison.OrdinalIgnoreCase) ||
               reason.StartsWith("Missing required query param", StringComparison.OrdinalIgnoreCase) ||
               reason.StartsWith("Missing required header", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFlakyExternal(int? statusCode, string? responseSnippet, string? failureReason)
    {
        if (statusCode == 502)
            return true;

        if (!string.IsNullOrWhiteSpace(responseSnippet) &&
            responseSnippet.Contains("502 Bad Gateway", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(failureReason) &&
            failureReason.Contains("but got 502", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
