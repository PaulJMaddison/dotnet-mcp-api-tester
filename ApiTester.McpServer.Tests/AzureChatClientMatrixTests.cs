using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.AI;
using ApiTester.AI.Azure;

namespace ApiTester.McpServer.Tests;

public sealed class AzureChatClientMatrixTests
{
    [Fact]
    public async Task SendsModelSystemUserAndOptionalCompletionLimitAndParsesUsage()
    {
        string? requestJson = null;
        var handler = new StaticHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""
            {"choices":[{"message":{"content":"grounded answer"}}],"usage":{"prompt_tokens":12,"completion_tokens":7}}
            """);
        });
        var options = Options(maxInputChars: 100, maxCompletionTokens: 321);
        var client = new AzureOpenAiClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        var result = await client.GetResponseAsync(new AiPrompt("system rules", "user question"));

        Assert.Equal("grounded answer", result.Content);
        Assert.Equal(12, result.Usage.InputTokens);
        Assert.Equal(7, result.Usage.OutputTokens);
        Assert.Equal("chat-test", result.Model);
        Assert.NotNull(requestJson);
        using var payload = JsonDocument.Parse(requestJson!);
        Assert.Equal("chat-test", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal(321, payload.RootElement.GetProperty("max_completion_tokens").GetInt32());
        var messages = payload.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("system rules", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("user question", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task InputBudgetTruncatesSystemFirstThenUsesRemainingBudgetForUser()
    {
        string? requestJson = null;
        var handler = new StaticHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"choices":[{"message":{"content":"ok"}}]}""");
        });
        var options = Options(maxInputChars: 10);
        var client = new AzureOpenAiClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        await client.GetResponseAsync(new AiPrompt("12345678", "ABCDEFGHIJ"));

        using var payload = JsonDocument.Parse(requestJson!);
        var messages = payload.RootElement.GetProperty("messages");
        Assert.Equal("12345678", messages[0].GetProperty("content").GetString());
        Assert.Equal("AB", messages[1].GetProperty("content").GetString());
        Assert.False(payload.RootElement.TryGetProperty("max_completion_tokens", out _));
    }

    [Fact]
    public async Task ArrayContentIsJoinedInProviderOrder()
    {
        var handler = new StaticHandler(_ => Task.FromResult(JsonResponse("""
        {"choices":[{"message":{"content":[{"text":"first"},"second",{"text":"third"}]}}],"usage":{"input_tokens":3,"output_tokens":4}}
        """)));
        var options = Options();
        var client = new AzureOpenAiClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        var result = await client.GetResponseAsync(new AiPrompt("s", "u"));

        Assert.Equal(string.Join(Environment.NewLine, "first", "second", "third"), result.Content);
        Assert.Equal(3, result.Usage.InputTokens);
        Assert.Equal(4, result.Usage.OutputTokens);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{}]}")]
    [InlineData("{\"choices\":[{\"message\":{}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"   \"}}]}")]
    public async Task MissingOrEmptyChatContentFailsClosed(string responseJson)
    {
        var handler = new StaticHandler(_ => Task.FromResult(JsonResponse(responseJson)));
        var options = Options();
        var client = new AzureOpenAiClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(new AiPrompt("s", "u")));
    }

    [Fact]
    public async Task MissingUsageProducesZeroUsageRatherThanInventedValues()
    {
        var handler = new StaticHandler(_ => Task.FromResult(JsonResponse("""{"choices":[{"message":{"content":"ok"}}]}""")));
        var options = Options();
        var client = new AzureOpenAiClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        var result = await client.GetResponseAsync(new AiPrompt("s", "u"));

        Assert.Equal(0, result.Usage.InputTokens);
        Assert.Equal(0, result.Usage.OutputTokens);
    }

    private static AzureOpenAiOptions Options(int maxInputChars = 1000, int maxCompletionTokens = 0) => new()
    {
        Endpoint = "https://example.openai.azure.com/openai/v1/",
        ChatDeployment = "chat-test",
        EmbeddingDeployment = "embedding-test",
        Authentication = AzureOpenAiOptions.ApiKeyAuthentication,
        ApiKey = "test-key",
        MaxRetries = 0,
        TimeoutSeconds = 5,
        MaxResponseBytes = 1024 * 1024,
        MaxInputChars = maxInputChars,
        MaxCompletionTokens = maxCompletionTokens,
        CircuitBreakerFailureThreshold = 4,
        CircuitBreakerBreakSeconds = 30
    };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
        public StaticHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _respond(request);
    }
}
