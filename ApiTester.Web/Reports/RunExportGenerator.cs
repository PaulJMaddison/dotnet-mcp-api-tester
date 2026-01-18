using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ApiTester.McpServer.Models;

namespace ApiTester.Web.Reports;

public static class RunExportGenerator
{
    private const int MaxPayloadLength = 2000;

    public static string GenerateJunit(TestRunRecord run)
    {
        var result = run.Result;
        var failures = result.Results.Count(r => !r.Pass && !r.Blocked);
        var skipped = result.Results.Count(r => r.Blocked);
        var suite = new XElement("testsuite",
            new XAttribute("name", $"{run.ProjectKey}:{result.OperationId}"),
            new XAttribute("tests", result.TotalCases),
            new XAttribute("failures", failures),
            new XAttribute("skipped", skipped),
            new XAttribute("time", FormatSeconds(result.TotalDurationMs)));

        foreach (var caseResult in result.Results)
        {
            var testCase = new XElement("testcase",
                new XAttribute("name", caseResult.Name),
                new XAttribute("classname", result.OperationId),
                new XAttribute("time", FormatSeconds(caseResult.DurationMs)));

            var details = BuildDetails(caseResult);
            if (caseResult.Blocked)
            {
                var message = string.IsNullOrWhiteSpace(caseResult.BlockReason)
                    ? "Blocked"
                    : TrimLargeText(caseResult.BlockReason);
                testCase.Add(new XElement("skipped", new XAttribute("message", message)));
            }
            else if (!caseResult.Pass)
            {
                var message = string.IsNullOrWhiteSpace(caseResult.FailureReason)
                    ? "Failed"
                    : TrimLargeText(caseResult.FailureReason);
                var failure = new XElement("failure", new XAttribute("message", message));
                if (!string.IsNullOrWhiteSpace(details) && details != "-")
                    failure.Add(new XCData(details));
                testCase.Add(failure);
            }

            if (!string.IsNullOrWhiteSpace(details) && details != "-" && caseResult.Pass)
                testCase.Add(new XElement("system-out", details));

            suite.Add(testCase);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), suite);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    public static string GenerateCsv(TestRunRecord run)
    {
        var builder = new StringBuilder();
        builder.AppendLine("case_name,status,duration_ms,status_code,classification,details");

        foreach (var caseResult in run.Result.Results)
        {
            var status = caseResult.Blocked
                ? "Blocked"
                : caseResult.Pass
                    ? "Passed"
                    : "Failed";
            var classification = ResultClassificationRules.Classify(caseResult).ToString();
            var details = BuildDetails(caseResult);

            builder.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(caseResult.Name),
                EscapeCsv(status),
                EscapeCsv(caseResult.DurationMs.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(caseResult.StatusCode?.ToString() ?? string.Empty),
                EscapeCsv(classification),
                EscapeCsv(details)
            }));
        }

        return builder.ToString();
    }

    public static string GenerateJson(TestRunRecord run, JsonSerializerOptions options)
    {
        var payload = new
        {
            run.RunId,
            run.ProjectKey,
            run.OperationId,
            run.StartedUtc,
            run.CompletedUtc,
            run.Environment,
            run.PolicySnapshot,
            run.Result
        };

        return JsonSerializer.Serialize(payload, options);
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

        if (result.StatusCode is not null)
            details.Add($"Status: {result.StatusCode}");

        return details.Count == 0 ? "-" : string.Join("\n", details);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string FormatSeconds(long durationMs)
        => (durationMs / 1000d).ToString("0.###", CultureInfo.InvariantCulture);

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
