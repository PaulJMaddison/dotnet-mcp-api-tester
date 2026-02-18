using System.Text.RegularExpressions;
using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Services;

public sealed class RedactionService
{
    private const string RedactedValue = "[REDACTED]";

    private static readonly HashSet<string> AuthHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "X-Api-Key",
        "Api-Key",
        "X-Auth-Token"
    };

    public TestPlan RedactPlan(TestPlan plan, IReadOnlyList<string>? patterns)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));

        var redactedCases = plan.Cases.Select(tc => new TestCase
        {
            Name = tc.Name,
            PathParams = new Dictionary<string, string>(tc.PathParams, StringComparer.OrdinalIgnoreCase),
            QueryParams = new Dictionary<string, string>(tc.QueryParams, StringComparer.OrdinalIgnoreCase),
            Headers = RedactHeaders(tc.Headers, patterns),
            BodyJson = RedactText(tc.BodyJson, patterns),
            ExpectedStatusCodes = tc.ExpectedStatusCodes.ToList()
        }).ToList();

        return new TestPlan
        {
            OperationId = plan.OperationId,
            Method = plan.Method,
            PathTemplate = plan.PathTemplate,
            Cases = redactedCases
        };
    }

    public TestRunResult RedactResult(TestRunResult result, IReadOnlyList<string>? patterns)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));

        var redactedResults = result.Results.Select(r => new TestCaseResult
        {
            Name = r.Name,
            Blocked = r.Blocked,
            BlockReason = r.BlockReason,
            Url = r.Url,
            Method = r.Method,
            StatusCode = r.StatusCode,
            DurationMs = r.DurationMs,
            Pass = r.Pass,
            FailureReason = r.FailureReason,
            FailureDetails = r.FailureDetails,
            ValidationUnavailableReason = r.ValidationUnavailableReason,
            ResponseSnippet = RedactText(r.ResponseSnippet, patterns),
            IsFlaky = r.IsFlaky,
            FlakeReasonCategory = r.FlakeReasonCategory,
            Classification = r.Classification
        }).ToList();

        return new TestRunResult
        {
            OperationId = result.OperationId,
            TotalCases = result.TotalCases,
            Passed = result.Passed,
            Failed = result.Failed,
            Blocked = result.Blocked,
            TotalDurationMs = result.TotalDurationMs,
            ClassificationSummary = result.ClassificationSummary,
            Results = redactedResults
        };
    }

    public Dictionary<string, string> RedactHeaders(
        IReadOnlyDictionary<string, string> headers,
        IReadOnlyList<string>? patterns)
    {
        var redacted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in headers)
        {
            var value = AuthHeaders.Contains(kvp.Key)
                ? RedactedValue
                : RedactText(kvp.Value, patterns) ?? kvp.Value;
            redacted[kvp.Key] = value;
        }

        return redacted;
    }

    public string? RedactText(string? text, IReadOnlyList<string>? patterns)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (patterns is null || patterns.Count == 0)
            return text;

        var redacted = text;
        foreach (var regex in BuildPatterns(patterns))
            redacted = regex.Replace(redacted, RedactedValue);

        return redacted;
    }

    private static IReadOnlyList<Regex> BuildPatterns(IReadOnlyList<string> patterns)
    {
        var list = new List<Regex>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            try
            {
                list.Add(new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                var quotedKeyPattern = pattern.Replace("\\s*[:=]", "\\\"?\\s*[:=]");
                if (!string.Equals(pattern, quotedKeyPattern, StringComparison.Ordinal))
                {
                    list.Add(new Regex(quotedKeyPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                }
            }
            catch (ArgumentException)
            {
                // Ignore invalid regex patterns
            }
        }

        return list;
    }
}
