using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiTester.McpServer.Rag;

namespace ApiTester.McpServer.Evals;

public sealed class EvalRunner
{
    private static readonly Regex Citation = new(@"\[chunk:[^\]]+\]", RegexOptions.Compiled);

    private readonly RagRuntime _rag;

    public EvalRunner(RagRuntime rag)
    {
        _rag = rag;
    }

    public async Task<EvalRunResult> RunAsync(Guid projectId, string evalSetPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evalSetPath))
            throw new ArgumentException("evalSetPath is required.", nameof(evalSetPath));

        if (!File.Exists(evalSetPath))
            throw new FileNotFoundException("Eval set file not found.", evalSetPath);

        var json = await File.ReadAllTextAsync(evalSetPath, ct);

        var (name, questions) = ParseEvalSet(json);

        if (questions.Count == 0)
        {
            var emptyMd = ToMarkdown(name, Array.Empty<EvalRow>());
            return new EvalRunResult(name, 0, emptyMd);
        }

        var rows = new List<EvalRow>(questions.Count);

        foreach (var q in questions)
        {
            ct.ThrowIfCancellationRequested();

            var result = await _rag.Answerer.AnswerAsync(projectId, q.Question, topK: 6, ct);

            var score = Score(q.Question, result.Answer, result.Evidence.Count);
            rows.Add(new EvalRow(q.Id, score.Total, score.Groundedness, score.Evidence, score.Coverage, result.Evidence.Count));
        }

        var markdown = ToMarkdown(name, rows);
        var overall = rows.Count == 0 ? 0 : rows.Average(r => r.Total);

        return new EvalRunResult(name, Math.Round(overall, 1), markdown);
    }

    private static (string Name, List<EvalQ> Questions) ParseEvalSet(string json)
    {
        // We parse loosely so evalset.demo.json can evolve without breaking the demo
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        var name =
            GetString(root, "name") ??
            GetString(root, "Name") ??
            "Demo Eval Set";

        var questionsArray =
            GetArray(root, "questions") ??
            GetArray(root, "Questions") ??
            GetArray(root, "items") ??
            GetArray(root, "Items");

        var list = new List<EvalQ>();

        if (questionsArray is not null)
        {
            foreach (var item in questionsArray.Value.EnumerateArray())
            {
                var id =
                    GetString(item, "id") ??
                    GetString(item, "Id") ??
                    Guid.NewGuid().ToString("N");

                var q =
                    GetString(item, "question") ??
                    GetString(item, "Question") ??
                    GetString(item, "prompt") ??
                    GetString(item, "Prompt");

                if (string.IsNullOrWhiteSpace(q))
                    continue;

                list.Add(new EvalQ(id, q));
            }
        }

        return (name, list);
    }

    private static JsonElement? GetArray(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(propertyName, out var el)) return null;
        return el.ValueKind == JsonValueKind.Array ? el : null;
    }

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(propertyName, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    private static EvalScore Score(string question, string answer, int evidenceCount)
    {
        var hasCitations = Citation.IsMatch(answer);

        var asksOps = question.Contains("operation", StringComparison.OrdinalIgnoreCase) ||
                     question.Contains("operationId", StringComparison.OrdinalIgnoreCase) ||
                     question.Contains("methods", StringComparison.OrdinalIgnoreCase) ||
                     question.Contains("paths", StringComparison.OrdinalIgnoreCase);

        var mentionsOperationId = answer.Contains("operationId", StringComparison.OrdinalIgnoreCase) ||
                                  answer.Contains("`", StringComparison.OrdinalIgnoreCase);

        var groundedness = hasCitations ? 1.0 : 0.0;
        var evidence = evidenceCount >= 2 ? 1.0 : evidenceCount == 1 ? 0.5 : 0.0;
        var coverage = !asksOps ? 1.0 : (mentionsOperationId ? 1.0 : 0.0);

        var total = Math.Round((groundedness * 0.5 + evidence * 0.2 + coverage * 0.3) * 100, 1);

        return new EvalScore(total, groundedness, evidence, coverage);
    }

    private static string ToMarkdown(string name, IReadOnlyList<EvalRow> rows)
    {
        var overall = rows.Count == 0 ? 0 : rows.Average(r => r.Total);

        var sb = new StringBuilder();
        sb.AppendLine($"# Eval Report, {name}");
        sb.AppendLine();
        sb.AppendLine($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine($"Overall score: **{overall:0.0}**");
        sb.AppendLine();
        sb.AppendLine("| Id | Score | Grounded | Evidence | Coverage | EvidenceChunks |");
        sb.AppendLine("|---:|------:|---------:|---------:|---------:|--------------:|");

        foreach (var r in rows)
        {
            sb.AppendLine($"| {r.Id} | {r.Total:0.0} | {r.Groundedness:0.0} | {r.Evidence:0.0} | {r.Coverage:0.0} | {r.EvidenceChunks} |");
        }

        sb.AppendLine();
        sb.AppendLine("## What this measures");
        sb.AppendLine("- Groundedness, answer includes citations like `[chunk:...]`.");
        sb.AppendLine("- Evidence, retrieval returns at least 2 chunks.");
        sb.AppendLine("- Coverage, answer mentions requested concepts (operationId, methods, paths).");
        sb.AppendLine();
        sb.AppendLine("## Next production step");
        sb.AppendLine("- Persist scores per day and track drift over time.");
        sb.AppendLine("- Add stricter checks, rubric scoring, refusal tests, prompt injection tests.");

        return sb.ToString();
    }

    public sealed record EvalRunResult(string Name, double OverallScore, string Markdown);

    private sealed record EvalQ(string Id, string Question);
    private sealed record EvalRow(string Id, double Total, double Groundedness, double Evidence, double Coverage, int EvidenceChunks);
    private sealed record EvalScore(double Total, double Groundedness, double Evidence, double Coverage);
}
