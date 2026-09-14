using System.Net;
using System.Text;
using ApiTester.AI;
using ApiTester.AI.Azure;
using ApiTester.McpServer.Rag;
using Azure.Core;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class AzureOpenAiIdentityTests
{
    [Fact]
    public async Task DefaultAzureCredentialMode_UsesBearerAndCorrectScopeWithoutLeakingToken()
    {
        var credential = new RecordingCredential("identity-token");
        var handler = new RecordingHandler(_ => Json("{\"ok\":true}"));
        var options = Options(
            authentication: AzureOpenAiOptions.DefaultAzureCredentialAuthentication,
            apiKey: "unused-key",
            bearer: "unused-bearer");
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options, credential);

        await transport.PostJsonAsync("chat/completions", new { message = "safe-body" }, CancellationToken.None);

        Assert.Equal("Bearer identity-token", handler.LastAuthorization);
        Assert.Null(handler.LastApiKey);
        Assert.Equal(AzureOpenAiTransport.AzureAiTokenScope, Assert.Single(credential.LastScopes));
        Assert.DoesNotContain("identity-token", handler.LastBody);
    }

    [Fact]
    public async Task ExplicitApiKeyMode_DoesNotAcquireTokenAndOverridesOtherCredentialValues()
    {
        var credential = new RecordingCredential("unused-identity-token");
        var handler = new RecordingHandler(_ => Json("{\"ok\":true}"));
        var options = Options(
            authentication: AzureOpenAiOptions.ApiKeyAuthentication,
            apiKey: "explicit-api-key",
            bearer: "unused-bearer");
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options, credential);

        await transport.PostJsonAsync("embeddings", new { input = "customers" }, CancellationToken.None);

        Assert.Equal("explicit-api-key", handler.LastApiKey);
        Assert.Null(handler.LastAuthorization);
        Assert.Equal(0, credential.CallCount);
    }

    [Fact]
    public async Task IdentityMode_IsDeterministicAndDoesNotLeakTokenInFailure()
    {
        var credential = new RecordingCredential("sensitive-identity-token");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("sensitive-identity-token provider body")
        });
        var options = Options(
            authentication: AzureOpenAiOptions.DefaultAzureCredentialAuthentication,
            apiKey: "unused-key",
            bearer: "unused-bearer");
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options, credential);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { message = "safe-body" }, CancellationToken.None));

        Assert.Equal("Bearer sensitive-identity-token", handler.LastAuthorization);
        Assert.DoesNotContain("sensitive-identity-token", exception.ToString());
    }

    [Fact]
    public async Task MissingCredentialForExplicitMode_FailsClearlyWithoutNetworkCall()
    {
        var handler = new RecordingHandler(_ => Json("{\"ok\":true}"));
        var transport = new AzureOpenAiTransport(
            new HttpClient(handler),
            Options(authentication: AzureOpenAiOptions.ApiKeyAuthentication, apiKey: ""));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, CancellationToken.None));

        Assert.Contains("authentication is not configured", exception.InnerException?.Message ?? exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CancellationDuringTokenAcquisition_PropagatesWithoutNetworkCall()
    {
        var credential = new BlockingCredential();
        var handler = new RecordingHandler(_ => Json("{\"ok\":true}"));
        var transport = new AzureOpenAiTransport(
            new HttpClient(handler),
            Options(authentication: AzureOpenAiOptions.DefaultAzureCredentialAuthentication),
            credential);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            transport.PostJsonAsync("chat/completions", new { }, cts.Token));

        Assert.Equal(AzureOpenAiTransport.AzureAiTokenScope, Assert.Single(credential.LastScopes));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ChatAndEmbeddings_ShareIdentityAuthenticatedTransport()
    {
        var credential = new RecordingCredential("shared-token");
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/embeddings", StringComparison.Ordinal)
                ? Json("{\"data\":[{\"embedding\":[0.1,0.2]}]}")
                : Json("{\"choices\":[{\"message\":{\"content\":\"grounded [chunk:1]\"}}]}"));
        var options = Options(
            authentication: AzureOpenAiOptions.DefaultAzureCredentialAuthentication,
            chat: "chat-deployment",
            embedding: "embedding-deployment");
        var transport = new AzureOpenAiTransport(new HttpClient(handler), options, credential);
        var chat = new AzureOpenAiClient(transport, options);
        var embeddings = new AzureOpenAiEmbeddingClient(transport, options);

        var chatResult = await chat.GetResponseAsync(new AiPrompt("system", "question"));
        var embeddingResult = await embeddings.EmbedAsync("GET /customers", CancellationToken.None);

        Assert.Equal("grounded [chunk:1]", chatResult.Content);
        Assert.Equal(new[] { 0.1f, 0.2f }, embeddingResult);
        Assert.Equal(2, handler.CallCount);
        Assert.All(handler.Authorizations, value => Assert.Equal("Bearer shared-token", value));
        Assert.Equal(2, credential.CallCount);
    }

    private static AzureOpenAiOptions Options(
        string authentication,
        string apiKey = "",
        string bearer = "",
        string chat = "chat",
        string embedding = "embed") => new()
    {
        Endpoint = "https://example.openai.azure.com",
        Authentication = authentication,
        ApiKey = apiKey,
        BearerToken = bearer,
        ChatDeployment = chat,
        EmbeddingDeployment = embedding,
        MaxRetries = 0,
        TimeoutSeconds = 5
    };

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingCredential(string token) : TokenCredential
    {
        public int CallCount { get; private set; }
        public string[] LastScopes { get; private set; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastScopes = requestContext.Scopes.ToArray();
            return ValueTask.FromResult(new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    private sealed class BlockingCredential : TokenCredential
    {
        public string[] LastScopes { get; private set; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            LastScopes = requestContext.Scopes.ToArray();
            return new ValueTask<AccessToken>(WaitForCancellationAsync(cancellationToken));
        }

        private static async Task<AccessToken> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? LastApiKey { get; private set; }
        public string? LastAuthorization { get; private set; }
        public string LastBody { get; private set; } = string.Empty;
        public List<string?> Authorizations { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastApiKey = request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null;
            LastAuthorization = request.Headers.Authorization?.ToString();
            Authorizations.Add(LastAuthorization);
            LastBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }
}
