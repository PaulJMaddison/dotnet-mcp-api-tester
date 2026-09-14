using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.AI.Azure;
using ApiTester.McpServer.Rag;

namespace ApiTester.McpServer.Tests;

public sealed class AzureAiMatrixTests
{
    [Theory]
    [InlineData("https://example.openai.azure.com", "https://example.openai.azure.com/openai/v1/")]
    [InlineData("https://example.openai.azure.com/", "https://example.openai.azure.com/openai/v1/")]
    [InlineData("https://example.openai.azure.com/openai/v1", "https://example.openai.azure.com/openai/v1/")]
    [InlineData("https://example.openai.azure.com/openai/v1/", "https://example.openai.azure.com/openai/v1/")]
    public void EndpointNormalisationIsDeterministic(string input, string expected)
    {
        var options = WithEndpoint(BaseOptions(), input);
        Assert.Equal(expected, options.GetApiBaseUri().ToString());
    }

    [Theory]
    [InlineData("http://example.openai.azure.com")]
    [InlineData("https://example.openai.azure.com/openai/v1?x=1")]
    [InlineData("https://example.openai.azure.com/openai/v1#fragment")]
    [InlineData("not-a-uri")]
    public void InvalidEndpointsFailClosed(string endpoint)
    {
        var options = WithEndpoint(BaseOptions(), endpoint);
        Assert.Throws<InvalidOperationException>(options.GetApiBaseUri);
    }

    [Fact]
    public void AuthenticationModeIsExplicitWhenConfiguredAndOtherwiseUsesAvailableSecret()
    {
        Assert.Equal(AzureOpenAiAuthenticationMode.ApiKey, WithAuthentication(BaseOptions(), AzureOpenAiOptions.ApiKeyAuthentication).GetAuthenticationMode());
        Assert.Equal(AzureOpenAiAuthenticationMode.BearerToken, WithBearerOnly(BaseOptions(), "token").GetAuthenticationMode());
        Assert.Equal(AzureOpenAiAuthenticationMode.ApiKey, WithApiKeyOnly(BaseOptions(), "key").GetAuthenticationMode());
        Assert.Throws<InvalidOperationException>(() => WithAuthentication(BaseOptions(), "something-else").GetAuthenticationMode());
    }

    [Fact]
    public void InvalidCredentialSourceAndInvalidResourceBoundsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => WithCredentialSource(BaseOptions(), "VisualStudio").GetCredentialSource());
        Assert.Throws<InvalidOperationException>(() => WithTimeout(BaseOptions(), 0).ValidateCommon());
        Assert.Throws<InvalidOperationException>(() => WithRetries(BaseOptions(), -1).ValidateCommon());
        Assert.Throws<InvalidOperationException>(() => WithMaxResponseBytes(BaseOptions(), 0).ValidateCommon());
        Assert.Throws<InvalidOperationException>(() => WithMaxInputChars(BaseOptions(), 0).ValidateCommon());
        Assert.Throws<InvalidOperationException>(() => WithCircuitThreshold(BaseOptions(), 0).ValidateCommon());
    }

    [Fact]
    public async Task EmbeddingsSplitAtProviderBatchLimit()
    {
        var handler = new EmbeddingHandler();
        var options = WithMaxInputChars(BaseOptions(), 100_000);
        var client = new AzureOpenAiEmbeddingClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        var result = await client.EmbedBatchAsync(Enumerable.Range(0, 65).Select(i => $"item-{i}").ToArray(), CancellationToken.None);

        Assert.Equal(65, result.Count);
        Assert.Equal(new[] { 32, 32, 1 }, handler.BatchSizes);
    }

    [Fact]
    public async Task EmbeddingsAlsoSplitByAggregateCharacterBudgetAndTrimSingleOversizedInput()
    {
        var handler = new EmbeddingHandler();
        var options = WithMaxInputChars(BaseOptions(), 5);
        var client = new AzureOpenAiEmbeddingClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        await client.EmbedBatchAsync(new[] { "aaaa", "bbbb", "0123456789" }, CancellationToken.None);

        Assert.Equal(new[] { 1, 1, 1 }, handler.BatchSizes);
        Assert.Equal(new[] { 4, 4, 5 }, handler.InputLengths);
    }

    [Theory]
    [InlineData("{\"data\":[{\"index\":0,\"embedding\":[1,0]},{\"index\":0,\"embedding\":[0,1]}]}")]
    [InlineData("{\"data\":[{\"index\":2,\"embedding\":[1,0]},{\"index\":1,\"embedding\":[0,1]}]}")]
    [InlineData("{\"data\":[{\"index\":0,\"embedding\":[]},{\"index\":1,\"embedding\":[0,1]}]}")]
    [InlineData("{\"data\":[{\"index\":0,\"embedding\":[1,0]}]}")]
    public async Task InvalidEmbeddingProviderResponsesFailClosed(string responseJson)
    {
        var options = BaseOptions();
        var handler = new StaticHandler(_ => JsonResponse(HttpStatusCode.OK, responseJson));
        var client = new AzureOpenAiEmbeddingClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.EmbedBatchAsync(new[] { "one", "two" }, CancellationToken.None));
    }

    [Fact]
    public async Task TransportRetriesTransientResponseThenSucceeds()
    {
        var call = 0;
        var handler = new StaticHandler(_ =>
        {
            call++;
            if (call == 1)
            {
                var response = JsonResponse(HttpStatusCode.TooManyRequests, "{}");
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return response;
            }
            return JsonResponse(HttpStatusCode.OK, "{\"ok\":true}");
        });
        var options = WithRetries(BaseOptions(), 1);
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options);

        var response = await transport.PostJsonAsync("chat/completions", new { hello = "world" }, CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Contains("ok", Encoding.UTF8.GetString(response.Body));
    }

    [Fact]
    public async Task CircuitBreakerOpensAfterConfiguredConsecutiveFailures()
    {
        var handler = new StaticHandler(_ => JsonResponse(HttpStatusCode.BadRequest, "{}"));
        var options = WithRetries(WithCircuitThreshold(BaseOptions(), 2), 0);
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.PostJsonAsync("embeddings", new { input = "a" }, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.PostJsonAsync("embeddings", new { input = "b" }, CancellationToken.None));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.PostJsonAsync("embeddings", new { input = "c" }, CancellationToken.None));

        Assert.Contains("circuit breaker is open", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ApiKeyIsSentAsHeaderAndNeverPlacedInPayload()
    {
        string? apiKeyHeader = null;
        string? requestBody = null;
        var handler = new StaticHandler(request =>
        {
            request.Headers.TryGetValues("api-key", out var values);
            apiKeyHeader = values?.SingleOrDefault();
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.OK, "{}");
        });
        var options = BaseOptions();
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options);

        await transport.PostJsonAsync("chat/completions", new { message = "hello" }, CancellationToken.None);

        Assert.Equal("unit-test-key", apiKeyHeader);
        Assert.NotNull(requestBody);
        Assert.DoesNotContain("unit-test-key", requestBody!);
    }

    [Fact]
    public async Task OversizedAzureResponseIsRejected()
    {
        var handler = new StaticHandler(_ => JsonResponse(HttpStatusCode.OK, "123456789"));
        var options = WithMaxResponseBytes(BaseOptions(), 4);
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.PostJsonAsync("chat/completions", new { x = 1 }, CancellationToken.None));
    }

    private static AzureOpenAiOptions BaseOptions() => new()
    {
        Endpoint = "https://example.openai.azure.com/openai/v1/",
        ChatDeployment = "chat-test",
        EmbeddingDeployment = "embedding-test",
        Authentication = AzureOpenAiOptions.ApiKeyAuthentication,
        ApiKey = "unit-test-key",
        MaxRetries = 0,
        TimeoutSeconds = 5,
        MaxResponseBytes = 1024 * 1024,
        MaxInputChars = 1000,
        CircuitBreakerFailureThreshold = 4,
        CircuitBreakerBreakSeconds = 30
    };

    private static AzureOpenAiOptions WithEndpoint(AzureOpenAiOptions o, string value) => Copy(o, endpoint: value);
    private static AzureOpenAiOptions WithAuthentication(AzureOpenAiOptions o, string value) => Copy(o, authentication: value);
    private static AzureOpenAiOptions WithCredentialSource(AzureOpenAiOptions o, string value) => Copy(o, credentialSource: value);
    private static AzureOpenAiOptions WithTimeout(AzureOpenAiOptions o, int value) => Copy(o, timeout: value);
    private static AzureOpenAiOptions WithRetries(AzureOpenAiOptions o, int value) => Copy(o, retries: value);
    private static AzureOpenAiOptions WithMaxResponseBytes(AzureOpenAiOptions o, int value) => Copy(o, maxResponseBytes: value);
    private static AzureOpenAiOptions WithMaxInputChars(AzureOpenAiOptions o, int value) => Copy(o, maxInputChars: value);
    private static AzureOpenAiOptions WithCircuitThreshold(AzureOpenAiOptions o, int value) => Copy(o, circuitThreshold: value);
    private static AzureOpenAiOptions WithBearerOnly(AzureOpenAiOptions o, string value) => Copy(o, authentication: string.Empty, apiKey: string.Empty, bearer: value);
    private static AzureOpenAiOptions WithApiKeyOnly(AzureOpenAiOptions o, string value) => Copy(o, authentication: string.Empty, apiKey: value, bearer: string.Empty);

    private static AzureOpenAiOptions Copy(
        AzureOpenAiOptions o,
        string? endpoint = null,
        string? authentication = null,
        string? credentialSource = null,
        string? apiKey = null,
        string? bearer = null,
        int? timeout = null,
        int? retries = null,
        int? maxResponseBytes = null,
        int? maxInputChars = null,
        int? circuitThreshold = null) => new()
    {
        Endpoint = endpoint ?? o.Endpoint,
        ChatDeployment = o.ChatDeployment,
        EmbeddingDeployment = o.EmbeddingDeployment,
        Authentication = authentication ?? o.Authentication,
        CredentialSource = credentialSource ?? o.CredentialSource,
        ApiKey = apiKey ?? o.ApiKey,
        BearerToken = bearer ?? o.BearerToken,
        TimeoutSeconds = timeout ?? o.TimeoutSeconds,
        MaxRetries = retries ?? o.MaxRetries,
        MaxResponseBytes = maxResponseBytes ?? o.MaxResponseBytes,
        MaxInputChars = maxInputChars ?? o.MaxInputChars,
        MaxCompletionTokens = o.MaxCompletionTokens,
        CircuitBreakerFailureThreshold = circuitThreshold ?? o.CircuitBreakerFailureThreshold,
        CircuitBreakerBreakSeconds = o.CircuitBreakerBreakSeconds
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public int CallCount { get; private set; }
        public StaticHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_respond(request));
        }
    }

    private sealed class EmbeddingHandler : HttpMessageHandler
    {
        public List<int> BatchSizes { get; } = new();
        public List<int> InputLengths { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var text = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(text);
            var inputs = document.RootElement.GetProperty("input").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
            BatchSizes.Add(inputs.Length);
            InputLengths.AddRange(inputs.Select(x => x.Length));
            var data = inputs.Select((_, index) => new { index, embedding = new[] { 1f, (float)index } }).ToArray();
            return JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new { data }));
        }
    }
}
