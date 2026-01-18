using System.Security.Cryptography;
using System.Text;
using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Comparison;

public sealed class RunComparisonService
{
    public RunComparisonResponse Compare(TestRunRecord run, TestRunRecord baseline)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (baseline is null) throw new ArgumentNullException(nameof(baseline));

        var baselineRemaining = new List<TestCaseResult>(baseline.Result.Results);
        var runRemaining = new List<TestCaseResult>(run.Result.Results);

        var matches = new List<CaseMatch>();
        var renamedCases = new List<TestCaseRenameDto>();

        var baselineByName = baselineRemaining
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var runByName = runRemaining
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var (name, baselineGroup) in baselineByName)
        {
            if (baselineGroup.Count != 1)
                continue;

            if (!runByName.TryGetValue(name, out var runGroup))
                continue;

            if (runGroup.Count != 1)
                continue;

            var baselineCase = baselineGroup[0];
            var runCase = runGroup[0];
            matches.Add(new CaseMatch(baselineCase, runCase));
            baselineRemaining.Remove(baselineCase);
            runRemaining.Remove(runCase);
        }

        var baselineByEndpoint = baselineRemaining
            .GroupBy(BuildEndpointKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var runByEndpoint = runRemaining
            .GroupBy(BuildEndpointKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var (key, baselineGroup) in baselineByEndpoint)
        {
            if (baselineGroup.Count != 1)
                continue;

            if (!runByEndpoint.TryGetValue(key, out var runGroup))
                continue;

            if (runGroup.Count != 1)
                continue;

            var baselineCase = baselineGroup[0];
            var runCase = runGroup[0];
            var renamed = !string.Equals(baselineCase.Name, runCase.Name, StringComparison.OrdinalIgnoreCase);
            matches.Add(new CaseMatch(baselineCase, runCase));

            if (renamed)
            {
                renamedCases.Add(new TestCaseRenameDto(
                    baselineCase.Name,
                    runCase.Name,
                    runCase.Method,
                    runCase.Url));
            }

            baselineRemaining.Remove(baselineCase);
            runRemaining.Remove(runCase);
        }

        var newFailures = new List<TestCaseComparisonDto>();
        var fixedFailures = new List<TestCaseComparisonDto>();
        var passToFail = new List<TestCaseComparisonDto>();
        var failToPass = new List<TestCaseComparisonDto>();
        var blockedChanges = new List<TestCaseComparisonDto>();
        var statusChanges = new List<TestCaseStatusChangeDto>();
        var responseSnippetChanges = new List<TestCaseResponseSnippetChangeDto>();
        var durationDeltas = new List<TestCaseDurationDeltaDto>();

        foreach (var match in matches)
        {
            var baselineOutcome = GetOutcome(match.Baseline);
            var runOutcome = GetOutcome(match.Run);

            var delta = new TestCaseComparisonDto(
                match.Run.Name,
                match.Run.Method,
                match.Run.Url,
                baselineOutcome,
                runOutcome,
                match.Baseline.StatusCode,
                match.Run.StatusCode,
                match.Baseline.DurationMs,
                match.Run.DurationMs);

            if (runOutcome == TestCaseOutcome.Failed && baselineOutcome != TestCaseOutcome.Failed)
                newFailures.Add(delta);

            if (baselineOutcome == TestCaseOutcome.Failed && runOutcome != TestCaseOutcome.Failed)
                fixedFailures.Add(delta);

            if (baselineOutcome == TestCaseOutcome.Passed && runOutcome == TestCaseOutcome.Failed)
                passToFail.Add(delta);

            if (baselineOutcome == TestCaseOutcome.Failed && runOutcome == TestCaseOutcome.Passed)
                failToPass.Add(delta);

            if (baselineOutcome != runOutcome &&
                (baselineOutcome == TestCaseOutcome.Blocked || runOutcome == TestCaseOutcome.Blocked))
            {
                blockedChanges.Add(delta);
            }

            if (match.Baseline.StatusCode != match.Run.StatusCode)
            {
                statusChanges.Add(new TestCaseStatusChangeDto(
                    match.Run.Name,
                    match.Run.Method,
                    match.Run.Url,
                    match.Baseline.StatusCode,
                    match.Run.StatusCode));
            }

            if (HasSnippetChanged(match.Baseline.ResponseSnippet, match.Run.ResponseSnippet))
            {
                responseSnippetChanges.Add(new TestCaseResponseSnippetChangeDto(
                    match.Run.Name,
                    match.Run.Method,
                    match.Run.Url,
                    HashSnippet(match.Baseline.ResponseSnippet),
                    BuildSnippetPreview(match.Baseline.ResponseSnippet),
                    HashSnippet(match.Run.ResponseSnippet),
                    BuildSnippetPreview(match.Run.ResponseSnippet)));
            }

            durationDeltas.Add(new TestCaseDurationDeltaDto(
                match.Run.Name,
                match.Run.Method,
                match.Run.Url,
                match.Baseline.DurationMs,
                match.Run.DurationMs,
                match.Run.DurationMs - match.Baseline.DurationMs));
        }

        return new RunComparisonResponse(
            run.RunId,
            baseline.RunId,
            newFailures,
            fixedFailures,
            passToFail,
            failToPass,
            blockedChanges,
            statusChanges,
            responseSnippetChanges,
            durationDeltas,
            runRemaining,
            baselineRemaining,
            renamedCases);
    }

    private static string BuildEndpointKey(TestCaseResult result)
        => $"{result.Method ?? string.Empty}|{result.Url ?? string.Empty}";

    private static TestCaseOutcome GetOutcome(TestCaseResult result)
    {
        if (result.Blocked)
            return TestCaseOutcome.Blocked;

        return result.Pass ? TestCaseOutcome.Passed : TestCaseOutcome.Failed;
    }

    private static bool HasSnippetChanged(string? baselineSnippet, string? runSnippet)
    {
        var baseline = NormalizeSnippet(baselineSnippet);
        var run = NormalizeSnippet(runSnippet);
        return !string.Equals(baseline, run, StringComparison.Ordinal);
    }

    private static string? NormalizeSnippet(string? snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
            return null;

        return snippet.Trim();
    }

    private static string? BuildSnippetPreview(string? snippet)
    {
        var normalized = NormalizeSnippet(snippet);
        if (normalized is null)
            return null;

        const int maxLength = 160;
        if (normalized.Length <= maxLength)
            return normalized;

        return $"{normalized[..maxLength]}…";
    }

    private static string? HashSnippet(string? snippet)
    {
        var normalized = NormalizeSnippet(snippet);
        if (normalized is null)
            return null;

        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record CaseMatch(TestCaseResult Baseline, TestCaseResult Run);
}
