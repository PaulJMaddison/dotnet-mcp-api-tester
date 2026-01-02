using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ApiTester.AI.Cost;

namespace ApiTester.AI.Local;

public sealed class LocalGroundedAiClient : IAiClient
{
    private static readonly Regex ChunkTag = new(@"\[chunk:(?<id>[^\]]+)\]", RegexOptions.Compiled);

    public async Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();

        // Make it feel real in a workshop, also makes timing demoable
        await Task.Delay(180, ct);

        var answer = BuildGroundedAnswer(prompt.User);

        sw.Stop();
        var tokensIn = EstimateTokens(prompt.System) + EstimateTokens(prompt.User);
        var tokensOut = EstimateTokens(answer);
        var usage = new AiUsage(tokensIn, tokensOut);
        var cost = AiCostCalculator.Estimate("local-grounded-stub", tokensIn, tokensOut);

        return new AiResponse(
            Content: answer,
            Usage: usage,
            ElapsedMs: (int)sw.ElapsedMilliseconds,
            Model: "local-grounded-stub",
            Cost: cost);

    }

    private static string BuildGroundedAnswer(string userPrompt)
    {
        var chunkIds = ChunkTag.Matches(userPrompt)
            .Select(m => m.Groups["id"].Value)
            .Distinct()
            .ToList();

        if (chunkIds.Count == 0)
        {
            return "I do not have enough evidence to answer that. Index the project first so I have evidence chunks.";
        }

        var mappings = ExtractOpenApiMappings(userPrompt);
        var sb = new StringBuilder();

        sb.AppendLine("Using only the provided evidence snippets, here is what I can infer:");
        sb.AppendLine();

        if (mappings.Count == 0)
        {
            sb.AppendLine("I can see evidence was provided, but I cannot reliably extract the exact operation mappings from the snippets.");
            sb.AppendLine($"Please ask a narrower question, or provide more evidence. Evidence seen: {string.Join(", ", chunkIds.Select(id => $"[chunk:{id}]"))}");
            return sb.ToString();
        }

        sb.AppendLine("Operations and mappings:");
        foreach (var m in mappings.Take(12))
        {
            sb.AppendLine($"- {m.Method} {m.Path}, operationId `{m.OperationId}` {m.Citation}");
        }

        sb.AppendLine();
        sb.AppendLine("If you want, pick one operationId and I will list required params and responses using the same evidence only rules.");

        return sb.ToString();
    }

    private static List<(string Method, string Path, string OperationId, string Citation)> ExtractOpenApiMappings(string userPrompt)
    {
        // Deliberately simple. It is a demo stub, but it only uses evidence content.
        var results = new List<(string, string, string, string)>();

        var opMatches = Regex.Matches(userPrompt, @"""operationId""\s*:\s*""(?<op>[^""]+)""", RegexOptions.Compiled);
        var pathMatches = Regex.Matches(userPrompt, @"""(?<path>\/[^""]+)""\s*:\s*\{", RegexOptions.Compiled);

        var ops = opMatches.Select(m => m.Groups["op"].Value).Distinct().ToList();
        var paths = pathMatches.Select(m => m.Groups["path"].Value).Distinct().ToList();

        var chunkIds = ChunkTag.Matches(userPrompt)
            .Select(m => m.Groups["id"].Value)
            .Distinct()
            .ToList();

        var cite = chunkIds.Count > 0 ? $"[chunk:{chunkIds[0]}]" : "";

        // Try to detect method blocks near operationId, fallback to HTTP
        string GuessMethod(string op)
        {
            // naive, good enough for a workshop
            if (userPrompt.Contains(@"""get"":", StringComparison.OrdinalIgnoreCase)) return "GET";
            if (userPrompt.Contains(@"""post"":", StringComparison.OrdinalIgnoreCase)) return "POST";
            if (userPrompt.Contains(@"""put"":", StringComparison.OrdinalIgnoreCase)) return "PUT";
            if (userPrompt.Contains(@"""delete"":", StringComparison.OrdinalIgnoreCase)) return "DELETE";
            return "HTTP";
        }

        for (var i = 0; i < Math.Min(ops.Count, paths.Count); i++)
        {
            results.Add((GuessMethod(ops[i]), paths[i], ops[i], cite));
        }

        return results;
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Math.Max(1, text.Length / 4);
    }
}
