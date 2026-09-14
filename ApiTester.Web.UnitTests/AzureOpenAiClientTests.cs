using System.Net;
using System.Text;
using ApiTester.AI;
using ApiTester.AI.Azure;
using Azure.Core;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class AzureOpenAiClientTests
{
    [Fact]
    public async Task GetResponseAsync_UsesEntraBearerTokenAndDeploymentName()
    {
        var handler = new CapturingHandler();
        var options = new AzureOpenAiOptions
        {
            Endpoint = "https://example.openai.azure.com/openai/v1/",
            ChatDeployment = "test-deployment",
            ModelName = "test-model"
        };
        var client = new AzureOpenAiClient(new HttpClient(handler), options, new TestCredential());

        var result = await client.GetResponseAsync(new AiPrompt("system", "user"));

        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-token", handler.AuthorizationParameter);
        Assert.Contains("/openai/v1/chat/completions", handler.RequestUri);
        Assert.Contains("\"model\":\"test-deployment\"", handler.RequestBody);
        Assert.DoesNotContain("test-token", handler.RequestBody);
        Assert.Equal("ok", result.Content);
        Assert.Equal(11, result.Usage.InputTokens);
        Assert.Equal(7, result.Usage.OutputTokens);
        Assert.Equal("test-model", result.Model);
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("test-token", DateTimeOffset.UtcNow.AddMinutes(5));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string RequestUri { get; private set; } = string.Empty;
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestUri = request.RequestUri?.ToString() ?? string.Empty;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            const string json = """
                {
                  "choices": [{ "message": { "content": "ok" } }],
                  "usage": { "prompt_tokens": 11, "completion_tokens": 7 }
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
