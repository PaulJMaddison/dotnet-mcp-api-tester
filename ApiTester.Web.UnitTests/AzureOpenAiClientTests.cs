using System.Net;
using System.Text;
using ApiTester.AI;
using ApiTester.AI.Azure;
using ApiTester.McpServer.Rag;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class AzureOpenAiClientTests
{
    [Fact]
    public async Task ChatClient_UsesV1EndpointApiKeyAndDeploymentName()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "choices": [{ "message": { "content": "Grounded answer [chunk:1]" } }],
              "usage": { "prompt_tokens": 41, "completion_tokens": 9 }
            }
            """));
        var http = new HttpClient(handler);
        var options = Options(chat: "chat-demo");
        var transport = new AzureOpenAiTransport(http, options);
        var client = new AzureOpenAiClient(transport, options);

        var result = await client.GetResponseAsync(
            new AiPrompt("system rules", "user question"),
            CancellationToken.None);

        Assert.Equal("Grounded answer [chunk:1]", result.Content);
        Assert.Equal(41, result.Usage.InputTokens);
        Assert.Equal(9, result.Usage.OutputTokens);
        Assert.Equal("chat-demo", result.Model);
        Assert.Equal(new Uri("https://example.openai.azure.com/openai/v1/chat/completions"), handler.LastRequestUri);
        Assert.Equal("test-key", handler.LastApiKey);
        Assert.Contains("\"model\":\"chat-demo\"", handler.LastBody);
        Assert.Contains("system rules", handler.LastBody);
        Assert.Contains("user question", handler.LastBody);
    }

    [Fact]
    public async Task EmbeddingClient_UsesConfiguredEmbeddingDeployment()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {
              "data": [{ "embedding": [0.25, -0.5, 0.75] }]
            }
            """));
        var http = new HttpClient(handler);
        var options = Options(embedding: "embed-demo");
        var transport = new AzureOpenAiTransport(http, options);
        var client = new AzureOpenAiEmbeddingClient(transport, options);

        var result = await client.EmbedAsync("GET /customers/{id}", CancellationToken.None);

        Assert.Equal(new[] { 0.25f, -0.5f, 0.75f }, result);
        Assert.Equal(new Uri("https://example.openai.azure.com/openai/v1/embeddings"), handler.LastRequestUri);
        Assert.Contains("\"model\":\"embed-demo\"", handler.LastBody);
    }

    [Fact]
    public async Task Transport_RetriesRateLimitWithoutLeakingErrorBody()
    {
        var calls = 0;
        var handler = new RecordingHandler(_ =>
        {
            calls++;
            if (calls == 1)
            {
                var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("sensitive upstream error body", Encoding.UTF8, "text/plain")
                };
                throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return throttled;
            }

            return JsonResponse("{\"ok\":true}");
        });
        var options = Options(chat: "chat-demo", maxRetries: 1);
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options);

        var response = await transport.PostJsonAsync(
            "chat/completions",
            new { model = "chat-demo", messages = Array.Empty<object>() },
            CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.NotEmpty(response.Body);
    }

    private static AzureOpenAiOptions Options(
        string chat = "",
        string embedding = "",
        int maxRetries = 0,
        int maxCompletionTokens = 200) => new()
    {
        Endpoint = "https://example.openai.azure.com",
        ChatDeployment = chat,
        EmbeddingDeployment = embedding,
        ApiKey = "test-key",
        TimeoutSeconds = 5,
        MaxRetries = maxRetries,
        MaxCompletionTokens = maxCompletionTokens
    };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public Uri? LastRequestUri { get; private set; }
        public string? LastApiKey { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastApiKey = request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null;
            LastBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _respond(request);
        }
    }
}
