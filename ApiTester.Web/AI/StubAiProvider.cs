using System.Text.Json;
using ApiTester.McpServer.Serialization;

namespace ApiTester.Web.AI;

public sealed class StubAiProvider : IAiProvider
{
    public Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var content = request.UserPrompt.Contains(AiExplainSchemas.SchemaJson, StringComparison.Ordinal)
            ? JsonSerializer.Serialize(new
            {
                summary = "Stub summary",
                inputs = "Stub inputs",
                outputs = "Stub outputs",
                auth = "Stub auth",
                gotchas = Array.Empty<string>(),
                examples = Array.Empty<object>(),
                markdown = "Stub markdown"
            }, JsonDefaults.Default)
            : request.UserPrompt.Contains(AiSuggestTestsSchemas.SchemaJson, StringComparison.Ordinal)
                ? JsonSerializer.Serialize(new
                {
                    cases = new[]
                    {
                        new
                        {
                            name = "Stub case",
                            rationale = "Stub rationale",
                            @params = new
                            {
                                path = new { },
                                query = new { },
                                headers = new { }
                            },
                            expectedStatusRanges = new[] { "200-299" }
                        }
                }
            }, JsonDefaults.Default)
            : request.UserPrompt.Contains(AiRunSummarySchemas.SchemaJson, StringComparison.Ordinal)
                ? JsonSerializer.Serialize(new
                {
                    overallSummary = "Stub summary with no critical risk.",
                    topFailures = Array.Empty<object>(),
                    flakeAssessment = "No clear flake signals in stub data.",
                    regressionLikelihood = new
                    {
                        level = "low",
                        rationale = "Stub response does not indicate persistent regression."
                    },
                    recommendedNextActions = new[] { "Re-run to confirm." }
                }, JsonDefaults.Default)
                : JsonSerializer.Serialize(new
                {
                    insights = Array.Empty<object>()
                }, JsonDefaults.Default);

        return Task.FromResult(new AiResult(content, "stub"));
    }
}
