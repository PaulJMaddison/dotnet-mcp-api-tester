using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class ExecutionPolicyMatrixTests
{
    [Fact]
    public async Task DryRunBuildsEscapedPathQueryAndRedactsBearerToken()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"http://127.0.0.1:5055"}],"paths":{"/items/{id}":{"get":{"operationId":"getItem","responses":{"200":{"description":"OK"}}}}}}
        """);
        var runtime = new ApiRuntimeConfig();
        runtime.SetBearerToken("super-secret");
        var tools = new ExecuteTools(store, runtime, new StubHttpClientFactory(), new SsrfGuard());

        var json = await tools.ApiCallOperation(
            "getItem",
            pathParamsJson: "{\"id\":\"a/b\"}",
            queryParamsJson: "{\"q\":\"a&b c\"}",
            headersJson: "{\"X-Test\":\"yes\"}");

        Assert.Contains("\"dryRun\":true", json);
        Assert.Contains("a%2Fb", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("q=a%26b%20c", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted]", json);
        Assert.DoesNotContain("super-secret", json);
        Assert.Contains("X-Test", json);
    }

    [Fact]
    public async Task MissingPathParameterFailsBeforeAnyHttpCall()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"http://127.0.0.1:5055"}],"paths":{"/items/{id}":{"get":{"operationId":"getItem","responses":{"200":{"description":"OK"}}}}}}
        """);
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var tools = new ExecuteTools(store, new ApiRuntimeConfig(), new StubHttpClientFactory(handler), new SsrfGuard());

        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.ApiCallOperation("getItem"));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task LiveExecutionIsDenyByDefaultWithoutAllowlistedBaseUrl()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"http://127.0.0.1:5055"}],"paths":{"/items":{"get":{"operationId":"getItems","responses":{"200":{"description":"OK"}}}}}}
        """);
        var runtime = new ApiRuntimeConfig();
        runtime.Policy.DryRun = false;
        runtime.Policy.BlockLocalhost = false;
        runtime.Policy.BlockPrivateNetworks = false;
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var tools = new ExecuteTools(store, runtime, new StubHttpClientFactory(handler), new SsrfGuard());

        var json = await tools.ApiCallOperation("getItems");

        Assert.Contains("\"blocked\":true", json);
        Assert.Contains("deny-by-default", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task MethodAllowlistBlocksDisallowedVerbBeforeNetwork()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"http://127.0.0.1:5055"}],"paths":{"/items":{"post":{"operationId":"createItem","responses":{"201":{"description":"Created"}}}}}}
        """);
        var runtime = new ApiRuntimeConfig();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var tools = new ExecuteTools(store, runtime, new StubHttpClientFactory(handler), new SsrfGuard());

        var json = await tools.ApiCallOperation("createItem", bodyJson: "{}");

        Assert.Contains("Method not allowed", json);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task LiveAllowedLocalhostExecutesAndCapsResponseBody()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"http://127.0.0.1:5055"}],"paths":{"/items":{"get":{"operationId":"getItems","responses":{"200":{"description":"OK"}}}}}}
        """);
        var runtime = new ApiRuntimeConfig();
        runtime.Policy.DryRun = false;
        runtime.Policy.BlockLocalhost = false;
        runtime.Policy.BlockPrivateNetworks = false;
        runtime.Policy.MaxResponseBodyBytes = 5;
        runtime.Policy.AllowedBaseUrls.Add("http://127.0.0.1:5055");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("123456789", Encoding.UTF8, "text/plain")
        });
        var tools = new ExecuteTools(store, runtime, new StubHttpClientFactory(handler), new SsrfGuard());

        var json = await tools.ApiCallOperation("getItems");

        Assert.Equal(1, handler.CallCount);
        Assert.Contains("\"statusCode\":200", json);
        Assert.Contains("12345", json);
        Assert.Contains("truncated", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123456789", json);
    }

    [Fact]
    public async Task AllowlistPathPrefixRequiresRealPathBoundary()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"http://127.0.0.1:5055/apix"}],"paths":{"/items":{"get":{"operationId":"getItems","responses":{"200":{"description":"OK"}}}}}}
        """);
        var runtime = new ApiRuntimeConfig();
        runtime.Policy.DryRun = false;
        runtime.Policy.BlockLocalhost = false;
        runtime.Policy.BlockPrivateNetworks = false;
        runtime.Policy.AllowedBaseUrls.Add("http://127.0.0.1:5055/api");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var tools = new ExecuteTools(store, runtime, new StubHttpClientFactory(handler), new SsrfGuard());

        var json = await tools.ApiCallOperation("getItems");

        Assert.Contains("\"blocked\":true", json);
        Assert.Contains("not allowed", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task OversizedRequestBodyIsRejectedEvenInDryRun()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"http://127.0.0.1:5055"}],"paths":{"/items":{"post":{"operationId":"createItem","responses":{"201":{"description":"Created"}}}}}}
        """);
        var runtime = new ApiRuntimeConfig();
        runtime.Policy.AllowedMethods.Add("POST");
        runtime.Policy.MaxRequestBodyBytes = 4;
        var tools = new ExecuteTools(store, runtime, new StubHttpClientFactory(), new SsrfGuard());

        await Assert.ThrowsAsync<InvalidOperationException>(() => tools.ApiCallOperation("createItem", bodyJson: "{\"a\":1}"));
    }

    [Fact]
    public async Task RuntimeBaseUrlOverridesOpenApiServer()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"https://example.invalid"}],"paths":{"/items":{"get":{"operationId":"getItems","responses":{"200":{"description":"OK"}}}}}}
        """);
        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("http://127.0.0.1:5055/custom");
        var tools = new ExecuteTools(store, runtime, new StubHttpClientFactory(), new SsrfGuard());

        var json = await tools.ApiCallOperation("getItems");

        Assert.Contains("http://127.0.0.1:5055/custom/items", json);
        Assert.DoesNotContain("example.invalid", json);
    }

    [Fact]
    public async Task RelativeOpenApiServerIsResolvedAgainstRemoteContractSource()
    {
        var store = Store("""
        {"openapi":"3.0.1","info":{"title":"Exec","version":"1"},"servers":[{"url":"/api/v3"}],"paths":{"/items/{id}":{"get":{"operationId":"getItem","responses":{"200":{"description":"OK"}}}}}}
        """, "https://petstore.example/api/v3/openapi.json");
        var tools = new ExecuteTools(store, new ApiRuntimeConfig(), new StubHttpClientFactory(), new SsrfGuard());

        var json = await tools.ApiCallOperation("getItem", pathParamsJson: "{\"id\":\"1\"}");

        Assert.Contains("https://petstore.example/api/v3/items/1", json);
    }

    [Fact]
    public void PolicyMutationDisabledCannotChangeSafetyState()
    {
        var runtime = new ApiRuntimeConfig();
        var tools = new PolicyTools(runtime, new McpSafetyOptions(false));

        var json = JsonSerializer.Serialize(tools.ApiSetPolicy("{\"dryRun\":false,\"blockLocalhost\":false}"));

        Assert.Contains("disabled", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(runtime.Policy.DryRun);
        Assert.True(runtime.Policy.BlockLocalhost);
    }

    [Fact]
    public void PolicyMutationValidatesClampsAndAppliesValuesAtomically()
    {
        var runtime = new ApiRuntimeConfig();
        var tools = new PolicyTools(runtime, new McpSafetyOptions(true));

        var result = JsonSerializer.Serialize(tools.ApiSetPolicy("""
        {"dryRun":false,"blockLocalhost":false,"blockPrivateNetworks":false,"timeoutSeconds":999,"maxRequestBodyBytes":123,"maxResponseBodyBytes":456,"allowedMethods":["post","get"],"allowedBaseUrls":["http://127.0.0.1:5055/"]}
        """));

        Assert.Contains("\"ok\":true", result);
        Assert.False(runtime.Policy.DryRun);
        Assert.False(runtime.Policy.BlockLocalhost);
        Assert.Equal(TimeSpan.FromSeconds(60), runtime.Policy.Timeout);
        Assert.Equal(123, runtime.Policy.MaxRequestBodyBytes);
        Assert.Equal(456, runtime.Policy.MaxResponseBodyBytes);
        Assert.Contains("POST", runtime.Policy.AllowedMethods);
        Assert.Contains("GET", runtime.Policy.AllowedMethods);
        Assert.Equal("http://127.0.0.1:5055", Assert.Single(runtime.Policy.AllowedBaseUrls));
    }

    [Fact]
    public void InvalidPolicyDoesNotPartiallyApplyEarlierFields()
    {
        var runtime = new ApiRuntimeConfig();
        var tools = new PolicyTools(runtime, new McpSafetyOptions(true));

        var json = JsonSerializer.Serialize(tools.ApiSetPolicy("{\"dryRun\":false,\"maxRequestBodyBytes\":-1}"));

        Assert.Contains("isError", json);
        Assert.True(runtime.Policy.DryRun);
        Assert.Equal(262_144, runtime.Policy.MaxRequestBodyBytes);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("relative/path")]
    [InlineData("not a url")]
    public void RuntimeBaseUrlRejectsNonHttpAbsoluteUrls(string input)
    {
        var runtime = new ApiRuntimeConfig();
        var result = JsonSerializer.Serialize(new RuntimeTools(runtime).ApiSetBaseUrl(input));

        Assert.Contains("isError", result);
        Assert.Null(runtime.BaseUrl);
    }

    [Fact]
    public void BearerTokenLivesOnlyInRuntimeAndCanBeCleared()
    {
        var runtime = new ApiRuntimeConfig();
        var tools = new RuntimeTools(runtime);

        tools.ApiSetBearerToken("secret");
        Assert.Equal("secret", runtime.BearerToken);
        tools.ApiClearAuth();
        Assert.Null(runtime.BearerToken);
    }

    private static OpenApiStore Store(string json, string source = "test")
    {
        var document = new OpenApiStringReader().Read(json, out var diagnostics);
        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);
        OpenApiOperationIdentity.EnsureOperationIds(document);
        var store = new OpenApiStore();
        store.SetDocument(Guid.NewGuid(), Guid.NewGuid(), document, source, "hash", DateTime.UtcNow);
        return store;
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler? handler = null) => _handler = handler ?? new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public int CallCount { get; private set; }
        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_respond(request));
        }
    }
}
