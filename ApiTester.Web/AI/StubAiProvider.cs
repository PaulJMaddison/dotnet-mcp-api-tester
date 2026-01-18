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
            : JsonSerializer.Serialize(new
            {
                insights = Array.Empty<object>()
            }, JsonDefaults.Default);

        return Task.FromResult(new AiResult(content, "stub"));
    }
}
