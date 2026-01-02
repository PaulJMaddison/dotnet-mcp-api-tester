using ApiTester.AI;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class AiClientTests
{
    [Fact]
    public async Task MockAiClient_CapturesPrompts_AndReturnsResponse()
    {
        var mock = new MockAiClient(_ => new AiResponse(
            Content: "ok",
            Usage: new AiUsage(10, 20),
            ElapsedMs: 5,
            Model: "mock",
            Cost: new AiCostEstimate(0.001m, 0.002m, 0.003m)));

        var prompt = new AiPrompt("system", "user");

        var resp = await mock.GetResponseAsync(prompt, CancellationToken.None);

        Assert.Equal("ok", resp.Content);
        Assert.Single(mock.ReceivedPrompts);
        Assert.Equal("system", mock.ReceivedPrompts.First().System);
        Assert.Equal("user", mock.ReceivedPrompts.First().User);
    }
}
