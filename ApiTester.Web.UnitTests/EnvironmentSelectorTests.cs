using ApiTester.McpServer.Services;
using ApiTester.Web.Execution;
using Microsoft.OpenApi.Models;

namespace ApiTester.Web.UnitTests;

public sealed class EnvironmentSelectorTests
{
    [Fact]
    public void TryApplyBaseUrl_UsesEnvironmentBaseUrl_WhenProvided()
    {
        var runtime = new ApiRuntimeConfig();
        var doc = new OpenApiDocument();

        var ok = EnvironmentSelector.TryApplyBaseUrl(runtime, doc, "https://api.example.com/v1/", out var baseUrl, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("https://api.example.com/v1", baseUrl);
        Assert.Equal("https://api.example.com/v1", runtime.BaseUrl);
    }

    [Fact]
    public void TryApplyBaseUrl_UsesRuntimeBaseUrl_WhenEnvironmentMissing()
    {
        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("https://runtime.example.com/");
        var doc = new OpenApiDocument();

        var ok = EnvironmentSelector.TryApplyBaseUrl(runtime, doc, null, out var baseUrl, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("https://runtime.example.com", baseUrl);
        Assert.Equal("https://runtime.example.com", runtime.BaseUrl);
    }

    [Fact]
    public void TryApplyBaseUrl_UsesOpenApiServer_WhenNoRuntimeOrEnvironment()
    {
        var runtime = new ApiRuntimeConfig();
        var doc = new OpenApiDocument
        {
            Servers = new List<OpenApiServer>
            {
                new() { Url = "https://server.example.com/api/" }
            }
        };

        var ok = EnvironmentSelector.TryApplyBaseUrl(runtime, doc, null, out var baseUrl, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal("https://server.example.com/api", baseUrl);
        Assert.Equal("https://server.example.com/api", runtime.BaseUrl);
    }

    [Fact]
    public void TryApplyBaseUrl_ReturnsError_WhenNoSourcesAvailable()
    {
        var runtime = new ApiRuntimeConfig();
        var doc = new OpenApiDocument();

        var ok = EnvironmentSelector.TryApplyBaseUrl(runtime, doc, null, out var baseUrl, out var error);

        Assert.False(ok);
        Assert.Null(baseUrl);
        Assert.Contains("OpenAPI spec does not define servers", error);
    }
}
