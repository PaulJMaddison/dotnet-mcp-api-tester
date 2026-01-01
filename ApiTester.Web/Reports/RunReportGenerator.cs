using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ApiTester.McpServer.Models;

namespace ApiTester.Web.Reports;

public enum RunReportFormat
{
    Markdown,
    Html
}

public static class RunReportGenerator
{
    private const int MaxPayloadLength = 2000;

    public static string Generate(TestRunRecord run, RunReportFormat format)
        => format switch
        {
            RunReportFormat.Markdown => GenerateMarkdown(run),
            RunReportFormat.Html => GenerateHtml(run),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported report format.")
        };

    private static string GenerateMarkdown(TestRunRecord run)
    {
        var builder = new StringBuilder();
        var result = run.Result;
        var classificationSummary = result.ClassificationSummary;

        builder.AppendLine("# Test Run Report");
        builder.AppendLine();
        builder.AppendLine($"- Run ID: `{run.RunId}`");
        builder.AppendLine($"- Project: `{EscapeMarkdown(run.ProjectKey)}`");
        builder.AppendLine($"- Operation: `{EscapeMarkdown(result.OperationId)}`");
        builder.AppendLine($"- Started (UTC): {run.StartedUtc:O}");
        builder.AppendLine($"- Completed (UTC): {run.CompletedUtc:O}");
        builder.AppendLine($"- Total Duration (ms): {result.TotalDurationMs}");
        builder.AppendLine();

        builder.AppendLine("## Summary");
        builder.AppendLine("| Metric | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Total Cases | {result.TotalCases} |");
        builder.AppendLine($"| Passed | {result.Passed} |");
        builder.AppendLine($"| Failed | {result.Failed} |");
        builder.AppendLine($"| Blocked | {result.Blocked} |");
        builder.AppendLine($"| Classification Pass | {classificationSummary.Pass} |");
        builder.AppendLine($"| Classification Fail | {classificationSummary.Fail} |");
        builder.AppendLine($"| Classification Blocked Expected | {classificationSummary.BlockedExpected} |");
        builder.AppendLine($"| Classification Blocked Unexpected | {classificationSummary.BlockedUnexpected} |");
        builder.AppendLine($"| Classification Flaky External | {classificationSummary.FlakyExternal} |");
        builder.AppendLine();

        builder.AppendLine("## Case Results");
        builder.AppendLine("| Case | Status | Duration (ms) | Status Code | Classification | Details |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");

        foreach (var caseResult in result.Results)
        {
            var classification = ResultClassificationRules.Classify(caseResult);
            builder.AppendLine($"| {EscapeMarkdown(caseResult.Name)} | {FormatStatus(caseResult)} | {caseResult.DurationMs} | {caseResult.StatusCode?.ToString() ?? "-"} | {classification} | {EscapeMarkdown(BuildDetails(caseResult))} |");
        }

        return builder.ToString();
    }

    private static string GenerateHtml(TestRunRecord run)
    {
        var builder = new StringBuilder();
        var result = run.Result;
        var classificationSummary = result.ClassificationSummary;
        var encoder = HtmlEncoder.Create(new TextEncoderSettings(
            UnicodeRanges.BasicLatin,
            UnicodeRanges.Latin1Supplement,
            UnicodeRanges.GeneralPunctuation));

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Test Run Report</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body { font-family: Arial, sans-serif; margin: 24px; }");
        builder.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 24px; }");
        builder.AppendLine("th, td { border: 1px solid #ccc; padding: 8px; text-align: left; vertical-align: top; }");
        builder.AppendLine("th { background-color: #f5f5f5; }");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<h1>Test Run Report</h1>");
        builder.AppendLine("<ul>");
        builder.AppendLine($"<li><strong>Run ID:</strong> {encoder.Encode(run.RunId.ToString())}</li>");
        builder.AppendLine($"<li><strong>Project:</strong> {encoder.Encode(run.ProjectKey)}</li>");
        builder.AppendLine($"<li><strong>Operation:</strong> {encoder.Encode(result.OperationId)}</li>");
        builder.AppendLine($"<li><strong>Started (UTC):</strong> {encoder.Encode(run.StartedUtc.ToString("O"))}</li>");
        builder.AppendLine($"<li><strong>Completed (UTC):</strong> {encoder.Encode(run.CompletedUtc.ToString("O"))}</li>");
        builder.AppendLine($"<li><strong>Total Duration (ms):</strong> {result.TotalDurationMs}</li>");
        builder.AppendLine("</ul>");

        builder.AppendLine("<h2>Summary</h2>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>Metric</th><th>Value</th></tr></thead>");
        builder.AppendLine("<tbody>");
        builder.AppendLine($"<tr><td>Total Cases</td><td>{result.TotalCases}</td></tr>");
        builder.AppendLine($"<tr><td>Passed</td><td>{result.Passed}</td></tr>");
        builder.AppendLine($"<tr><td>Failed</td><td>{result.Failed}</td></tr>");
        builder.AppendLine($"<tr><td>Blocked</td><td>{result.Blocked}</td></tr>");
        builder.AppendLine($"<tr><td>Classification Pass</td><td>{classificationSummary.Pass}</td></tr>");
        builder.AppendLine($"<tr><td>Classification Fail</td><td>{classificationSummary.Fail}</td></tr>");
        builder.AppendLine($"<tr><td>Classification Blocked Expected</td><td>{classificationSummary.BlockedExpected}</td></tr>");
        builder.AppendLine($"<tr><td>Classification Blocked Unexpected</td><td>{classificationSummary.BlockedUnexpected}</td></tr>");
        builder.AppendLine($"<tr><td>Classification Flaky External</td><td>{classificationSummary.FlakyExternal}</td></tr>");
        builder.AppendLine("</tbody>");
        builder.AppendLine("</table>");

        builder.AppendLine("<h2>Case Results</h2>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>Case</th><th>Status</th><th>Duration (ms)</th><th>Status Code</th><th>Classification</th><th>Details</th></tr></thead>");
        builder.AppendLine("<tbody>");

        foreach (var caseResult in result.Results)
        {
            var classification = ResultClassificationRules.Classify(caseResult);
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{encoder.Encode(caseResult.Name)}</td>");
            builder.AppendLine($"<td>{encoder.Encode(FormatStatus(caseResult))}</td>");
            builder.AppendLine($"<td>{caseResult.DurationMs}</td>");
            builder.AppendLine($"<td>{encoder.Encode(caseResult.StatusCode?.ToString() ?? "-")}</td>");
            builder.AppendLine($"<td>{encoder.Encode(classification.ToString())}</td>");
            builder.AppendLine($"<td>{encoder.Encode(BuildDetails(caseResult)).Replace("\n", "<br/>")}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody>");
        builder.AppendLine("</table>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }

    private static string FormatStatus(TestCaseResult result)
    {
        if (result.Blocked)
            return "Blocked";

        return result.Pass ? "Passed" : "Failed";
    }

    private static string BuildDetails(TestCaseResult result)
    {
        var details = new List<string>();

        if (result.Blocked && !string.IsNullOrWhiteSpace(result.BlockReason))
            details.Add($"Blocked: {TrimLargeText(result.BlockReason)}");

        if (!result.Pass && !result.Blocked && !string.IsNullOrWhiteSpace(result.FailureReason))
            details.Add($"Failure: {TrimLargeText(result.FailureReason)}");

        if (!string.IsNullOrWhiteSpace(result.ResponseSnippet))
            details.Add($"Response: {TrimLargeText(result.ResponseSnippet)}");

        if (!string.IsNullOrWhiteSpace(result.Url))
            details.Add($"Url: {TrimLargeText(result.Url)}");

        return details.Count == 0 ? "-" : string.Join("\n", details);
    }

    private static string EscapeMarkdown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        return value.Replace("|", "\\|").Replace("\r\n", "<br/>").Replace("\n", "<br/>");
    }

    private static string TrimLargeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var runeCount = 0;
        var builder = new StringBuilder();
        var truncated = false;

        foreach (var rune in value.EnumerateRunes())
        {
            if (runeCount >= MaxPayloadLength)
            {
                truncated = true;
                break;
            }

            builder.Append(rune.ToString());
            runeCount++;
        }

        if (!truncated)
            return value;

        builder.Append("… (truncated)");
        return builder.ToString();
    }
}
