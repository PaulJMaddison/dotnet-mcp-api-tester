using ApiTester.Site.Content;

namespace ApiTester.Site.Tests;

public class DocsContentTests
{
    [Fact]
    public void SearchIndex_IncludesExpectedEntries()
    {
        var entries = DocsContent.Current.SearchIndex;

        Assert.Contains(entries, entry => entry.Title.Contains("Getting started", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Title.Contains("Import OpenAPI", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Title.Contains("Policies", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Title.Contains("API reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApiReference_ContainsKeyEndpoints()
    {
        var endpoints = DocsContent.Current.ApiReference.Endpoints;

        Assert.Contains(endpoints, endpoint => endpoint.Method == "POST" && endpoint.Path == "/api/openapi/import");
        Assert.Contains(endpoints, endpoint => endpoint.Method == "GET" && endpoint.Path == "/api/projects");
        Assert.Contains(endpoints, endpoint => endpoint.Method == "POST" && endpoint.Path == "/api/projects/{projectId}/test-plans/run");
        Assert.Contains(endpoints, endpoint => endpoint.Method == "GET" && endpoint.Path == "/api/runs/{runId}");
    }
}
