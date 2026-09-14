using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApiTester.AI;
using ApiTester.AI.Azure;
using ApiTester.McpServer.Rag;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class AzureOpenAiEdgeCaseTests
{
    [Fact]
    public void Options_AppendsV1PathForAzureOpenAiEndpoint()
    {
        var options = Options(endpoint: "https://example.openai.azure.com/");
        Assert.Equal("https://example.openai.azure.com/openai/v1/", options.GetApiBaseUri().ToString());
    }

    [Fact]
    public void Options_AppendsV1PathForFoundryEndpoint()
    {
        var options = Options(endpoint: "https://example.services.ai.azure.com");
        Assert.Equal("https://example.services.ai.azure.com/openai/v1/", options.GetApiBaseUri().ToString());
    }

    [Fact]
    public void Options_DoesNotDuplicateExistingV1Path()
    {
        var options = Options(endpoint: "https://example.openai.azure.com/openai/v1/");
        Assert.Equal("https://example.openai.azure.com/openai/v1/", options.GetApiBaseUri().ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("/relative/path")]
    public void Options_InvalidEndpoint_Throws(string endpoint)
    {
        var options = Options(endpoint: endpoint);
        Assert.Throws<InvalidOperationException>(() => options.GetApiBaseUri());
    }

    [Fact]
    public void Options_HttpEndpoint_Throws()
    {
        var options = Options(endpoint: "http://example.openai.azure.com");
        Assert.Throws<InvalidOperationException>(() => options.GetApiBaseUri());
    }

    [Theory]
    [InlineData("https://example.openai.azure.com?api-version=x")]
    [InlineData("https://example.openai.azure.com#fragment")]
    public void Options_QueryOrFragment_Throws(string endpoint)
    {
        var options = Options(endpoint: endpoint);
        Assert.Throws<InvalidOperationException>(() => options.GetApiBaseUri());
    }

    [Fact]
    public void Options_ChatAndEmbeddingConfigurationAreIndependent()
    {
        var chatOnly = Options(chat: "chat", embedding: "");
        var embeddingOnly = Options(chat: "", embedding: "embed");

        Assert.True(chatOnly.IsChatConfigured);
        Assert.False(chatOnly.IsEmbeddingConfigured);
        Assert.False(embeddingOnly.IsChatConfigured);
        Assert.True(embeddingOnly.IsEmbeddingConfigured);
    }

    [Fact]
    public void Options_BearerTokenCountsAsCredential()
    {
        var options = Options(apiKey: "", bearer: "token", chat: "chat");
        Assert.True(options.HasCredentials);
        Assert.True(options.IsChatConfigured);
    }

    [Fact]
    public void Options_WhitespaceCredentialDoesNotCount()
    {
        var options = Options(apiKey: "   ", bearer: " ", chat: "chat");
        Assert.False(options.HasCredentials);
        Assert.False(options.IsChatConfigured);
    }

    [Fact]
    public void Options_InvalidNumericConfigurationFailsFast()
    {
        var invalid = new AzureOpenAiOptions[]
        {
            Options(timeoutSeconds: 0),
            Options(maxRetries: -1),
            Options(maxResponseBytes: 0),
            Options(maxInputChars: 0),
            Options(circuitThreshold: 0),
            Options(circuitBreakSeconds: 0)
        };

        Assert.All(invalid, options => Assert.Throws<InvalidOperationException>(options.ValidateCommon));
    }

    [Fact]
    public void ChatClient_RequiresCompleteChatConfiguration()
    {
        var options = Options(chat: "", embedding: "embed");
        var transport = new AzureOpenAiTransport(new HttpClient(new RecordingHandler(_ => Json("{}"))), options);

        Assert.Throws<InvalidOperationException>(() => new AzureOpenAiClient(transport, options));
    }

    [Fact]
    public void EmbeddingClient_RequiresCompleteEmbeddingConfiguration()
    {
        var options = Options(chat: "chat", embedding: "");
        var transport = new AzureOpenAiTransport(new HttpClient(new RecordingHandler(_ => Json("{}"))), options);

        Assert.Throws<InvalidOperationException>(() => new AzureOpenAiEmbeddingClient(transport, options));
    }

    [Fact]
    public void Transport_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new AzureOpenAiTransport(null!, Options()));
        Assert.Throws<ArgumentNullException>(() => new AzureOpenAiTransport(new HttpClient(), null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Transport_BlankRelativePath_ThrowsWithoutSending(string? path)
    {
        var handler = new RecordingHandler(_ => Json("{}"));
        var transport = Transport(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            transport.PostJsonAsync(path!, new { }, CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Transport_NullPayload_ThrowsWithoutSending()
    {
        var handler = new RecordingHandler(_ => Json("{}"));
        var transport = Transport(handler);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            transport.PostJsonAsync("chat/completions", null!, CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Transport_PreCancelledToken_DoesNotSend()
    {
        var handler = new RecordingHandler(_ => Json("{}"));
        var transport = Transport(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, cts.Token));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Transport_UsesTrimmedApiKey()
    {
        var handler = new RecordingHandler(_ => Json("{}"));
        var transport = Transport(handler, Options(apiKey: "  key-value  "));

        await transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None);

        Assert.Equal("key-value", handler.LastApiKey);
        Assert.Null(handler.LastAuthorization);
    }

    [Fact]
    public async Task Transport_BearerTokenTakesPrecedenceOverApiKey()
    {
        var handler = new RecordingHandler(_ => Json("{}"));
        var transport = Transport(handler, Options(apiKey: "key", bearer: "  bearer-token  "));

        await transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None);

        Assert.Equal("Bearer bearer-token", handler.LastAuthorization);
        Assert.Null(handler.LastApiKey);
    }

    [Fact]
    public async Task Transport_MissingCredentialsFailsBeforeNetworkCall()
    {
        var handler = new RecordingHandler(_ => Json("{}"));
        var transport = Transport(handler, Options(apiKey: "", bearer: ""));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));

        Assert.Equal("Azure OpenAI request failed.", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Transport_NonTransientFailureDoesNotRetryOrLeakBody()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("secret-provider-error-body")
            };
            response.Headers.TryAddWithoutValidation("apim-request-id", "req-123");
            return response;
        });
        var transport = Transport(handler, Options(maxRetries: 3));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.DoesNotContain("secret-provider-error-body", ex.ToString());
        var inner = Assert.IsType<AzureOpenAiRequestException>(ex.InnerException);
        Assert.Equal(HttpStatusCode.BadRequest, inner.StatusCode);
        Assert.Equal("req-123", inner.RequestId);
    }

    [Fact]
    public async Task Transport_CapturesRequestIdOnSuccess()
    {
        var handler = new RecordingHandler(_ =>
        {
            var response = Json("{\"ok\":true}");
            response.Headers.TryAddWithoutValidation("x-request-id", "success-42");
            return response;
        });
        var transport = Transport(handler);

        var response = await transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None);

        Assert.Equal("success-42", response.RequestId);
        Assert.True(response.ElapsedMs >= 0);
    }

    [Fact]
    public async Task Transport_ResponseLargerThanConfiguredLimitFails()
    {
        var handler = new RecordingHandler(_ => Json(new string('x', 100)));
        var transport = Transport(handler, Options(maxResponseBytes: 20));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));

        Assert.Contains("configured maximum", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task Transport_CircuitOpensAfterConfiguredConsecutiveFailures()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var transport = Transport(handler, Options(maxRetries: 0, circuitThreshold: 1, circuitBreakSeconds: 60));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));

        var second = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));

        Assert.Contains("circuit breaker is open", second.Message);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Transport_SuccessResetsConsecutiveFailureCount()
    {
        var sequence = new Queue<HttpStatusCode>(new[]
        {
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK
        });
        var handler = new RecordingHandler(_ =>
        {
            var status = sequence.Dequeue();
            return status == HttpStatusCode.OK ? Json("{}") : new HttpResponseMessage(status);
        });
        var transport = Transport(handler, Options(maxRetries: 0, circuitThreshold: 2));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));
        await transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));
        await transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None);

        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task ChatClient_NullPrompt_ThrowsWithoutSending()
    {
        var handler = new RecordingHandler(_ => ChatResponse("unused"));
        var client = ChatClient(handler);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.GetResponseAsync(null!, CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ChatClient_PreCancelledToken_DoesNotSend()
    {
        var handler = new RecordingHandler(_ => ChatResponse("unused"));
        var client = ChatClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetResponseAsync(new AiPrompt("system", "user"), cts.Token));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ChatClient_TruncatesCombinedSystemAndUserContextToConfiguredLimit()
    {
        var handler = new RecordingHandler(_ => ChatResponse("ok"));
        var options = Options(chat: "chat", maxInputChars: 10);
        var client = ChatClient(handler, options);

        await client.GetResponseAsync(new AiPrompt("12345678", "ABCDEFGHIJ"), CancellationToken.None);

        using var json = JsonDocument.Parse(handler.LastBody);
        var messages = json.RootElement.GetProperty("messages");
        Assert.Equal("12345678", messages[0].GetProperty("content").GetString());
        Assert.Equal("AB", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task ChatClient_OmitsMaxCompletionTokensWhenNotConfigured()
    {
        var handler = new RecordingHandler(_ => ChatResponse("ok"));
        var options = Options(chat: "chat", maxCompletionTokens: 0);
        var client = ChatClient(handler, options);

        await client.GetResponseAsync(new AiPrompt("system", "user"), CancellationToken.None);

        Assert.DoesNotContain("max_completion_tokens", handler.LastBody);
    }

    [Fact]
    public async Task ChatClient_IncludesMaxCompletionTokensWhenConfigured()
    {
        var handler = new RecordingHandler(_ => ChatResponse("ok"));
        var options = Options(chat: "chat", maxCompletionTokens: 321);
        var client = ChatClient(handler, options);

        await client.GetResponseAsync(new AiPrompt("system", "user"), CancellationToken.None);

        Assert.Contains("\"max_completion_tokens\":321", handler.LastBody);
    }

    [Fact]
    public async Task ChatClient_ParsesArrayContentAndSkipsNonTextParts()
    {
        var handler = new RecordingHandler(_ => Json(
            """
            {
              "choices": [{
                "message": {
                  "content": [
                    "first",
                    { "text": "second" },
                    { "image_url": "ignored" },
                    42,
                    "   "
                  ]
                }
              }]
            }
            """));
        var client = ChatClient(handler);

        var result = await client.GetResponseAsync(new AiPrompt("s", "u"), CancellationToken.None);

        Assert.Equal($"first{Environment.NewLine}second", result.Content);
    }

    [Fact]
    public async Task ChatClient_ParsesAlternativeUsageFieldNames()
    {
        var handler = new RecordingHandler(_ => Json(
            """
            {
              "choices": [{ "message": { "content": "ok" } }],
              "usage": { "input_tokens": 12, "output_tokens": 7 }
            }
            """));
        var client = ChatClient(handler);

        var result = await client.GetResponseAsync(new AiPrompt("s", "u"), CancellationToken.None);

        Assert.Equal(12, result.Usage.InputTokens);
        Assert.Equal(7, result.Usage.OutputTokens);
    }

    [Fact]
    public async Task ChatClient_MissingUsageDefaultsToZero()
    {
        var handler = new RecordingHandler(_ => ChatResponse("ok"));
        var client = ChatClient(handler);

        var result = await client.GetResponseAsync(new AiPrompt("s", "u"), CancellationToken.None);

        Assert.Equal(0, result.Usage.InputTokens);
        Assert.Equal(0, result.Usage.OutputTokens);
    }

    [Theory]
    [InlineData("{}", "no chat choices")]
    [InlineData("{\"choices\":[]}", "no chat choices")]
    [InlineData("{\"choices\":[{\"message\":{}}]}", "did not contain message content")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"   \"}}]}", "empty message content")]
    public async Task ChatClient_MalformedOrIncompleteSuccessfulResponsesFailClosed(string responseJson, string expectedMessage)
    {
        var handler = new RecordingHandler(_ => Json(responseJson));
        var client = ChatClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetResponseAsync(new AiPrompt("s", "u"), CancellationToken.None));

        Assert.Contains(expectedMessage, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatClient_InvalidJsonPropagatesJsonException()
    {
        var handler = new RecordingHandler(_ => Json("not-json"));
        var client = ChatClient(handler);

        await Assert.ThrowsAsync<JsonException>(() =>
            client.GetResponseAsync(new AiPrompt("s", "u"), CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmbeddingClient_BlankInput_ThrowsWithoutSending(string? text)
    {
        var handler = new RecordingHandler(_ => EmbeddingResponse(1f));
        var client = EmbeddingClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.EmbedAsync(text!, CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task EmbeddingClient_PreCancelledToken_DoesNotSend()
    {
        var handler = new RecordingHandler(_ => EmbeddingResponse(1f));
        var client = EmbeddingClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.EmbedAsync("customers", cts.Token));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task EmbeddingClient_TruncatesInputToConfiguredLimit()
    {
        var handler = new RecordingHandler(_ => EmbeddingResponse(1f, 2f));
        var options = Options(embedding: "embed", maxInputChars: 5);
        var client = EmbeddingClient(handler, options);

        await client.EmbedAsync("ABCDEFGHIJ", CancellationToken.None);

        using var json = JsonDocument.Parse(handler.LastBody);
        Assert.Equal("ABCDE", json.RootElement.GetProperty("input").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":[{}]}")]
    [InlineData("{\"data\":[{\"embedding\":\"not-an-array\"}]}")]
    public async Task EmbeddingClient_MissingVectorFailsClosed(string responseJson)
    {
        var handler = new RecordingHandler(_ => Json(responseJson));
        var client = EmbeddingClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.EmbedAsync("customers", CancellationToken.None));

        Assert.Contains("no embedding vector", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbeddingClient_EmptyVectorFailsClosed()
    {
        var handler = new RecordingHandler(_ => Json("{\"data\":[{\"embedding\":[]}]}"));
        var client = EmbeddingClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.EmbedAsync("customers", CancellationToken.None));

        Assert.Contains("empty embedding vector", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbeddingClient_NonNumericVectorValueFailsClosed()
    {
        var handler = new RecordingHandler(_ => Json("{\"data\":[{\"embedding\":[0.1,\"oops\"]}]}"));
        var client = EmbeddingClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.EmbedAsync("customers", CancellationToken.None));

        Assert.Contains("invalid embedding value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AzureOpenAiClient ChatClient(RecordingHandler handler, AzureOpenAiOptions? options = null)
    {
        options ??= Options(chat: "chat");
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options);
        return new AzureOpenAiClient(transport, options);
    }

    private static AzureOpenAiEmbeddingClient EmbeddingClient(RecordingHandler handler, AzureOpenAiOptions? options = null)
    {
        options ??= Options(embedding: "embed");
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options);
        return new AzureOpenAiEmbeddingClient(transport, options);
    }

    private static AzureOpenAiTransport Transport(RecordingHandler handler, AzureOpenAiOptions? options = null) =>
        new(new HttpClient(handler), options ?? Options());

    private static AzureOpenAiOptions Options(
        string endpoint = "https://example.openai.azure.com",
        string chat = "chat",
        string embedding = "embed",
        string apiKey = "key",
        string bearer = "",
        int timeoutSeconds = 5,
        int maxRetries = 0,
        int maxResponseBytes = 1_048_576,
        int maxInputChars = 10_000,
        int maxCompletionTokens = 0,
        int circuitThreshold = 4,
        int circuitBreakSeconds = 30) => new()
    {
        Endpoint = endpoint,
        ChatDeployment = chat,
        EmbeddingDeployment = embedding,
        ApiKey = apiKey,
        BearerToken = bearer,
        TimeoutSeconds = timeoutSeconds,
        MaxRetries = maxRetries,
        MaxResponseBytes = maxResponseBytes,
        MaxInputChars = maxInputChars,
        MaxCompletionTokens = maxCompletionTokens,
        CircuitBreakerFailureThreshold = circuitThreshold,
        CircuitBreakerBreakSeconds = circuitBreakSeconds
    };

    private static HttpResponseMessage ChatResponse(string content) => Json(
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        }));

    private static HttpResponseMessage EmbeddingResponse(params float[] values) => Json(
        JsonSerializer.Serialize(new
        {
            data = new[] { new { embedding = values } }
        }));

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
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

        public int CallCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string? LastApiKey { get; private set; }
        public string? LastAuthorization { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            LastApiKey = request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null;
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _respond(request);
        }
    }
}
