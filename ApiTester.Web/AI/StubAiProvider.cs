using System.Text.Json;
using ApiTester.McpServer.Serialization;

namespace ApiTester.Web.AI;

public sealed class StubAiProvider : IAiProvider
{
    public Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var payload = new
        {
            insights = Array.Empty<object>()
        };

        var content = JsonSerializer.Serialize(payload, JsonDefaults.Default);
        return Task.FromResult(new AiResult(content, "stub"));
    }
}
