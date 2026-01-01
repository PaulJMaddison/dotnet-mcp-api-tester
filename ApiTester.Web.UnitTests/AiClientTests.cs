using ApiTester.AI;

namespace ApiTester.Web.UnitTests;

public sealed class AiClientTests
{
    [Fact]
    public async Task MockAiClientTracksPromptsAndResponses()
    {
        var client = new MockAiClient(prompt => new AiResponse($"Echo: {prompt.User}"));
        var prompt = new AiPrompt("system", "user");

        var response = await client.GetResponseAsync(prompt);

        Assert.Equal("Echo: user", response.Content);
        Assert.Single(client.ReceivedPrompts);
        Assert.Equal(prompt, client.ReceivedPrompts[0]);
    }
}
