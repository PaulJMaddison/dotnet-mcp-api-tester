using System.Text.Json;
using ApiTester.AI.Azure;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class PublicApiRegressionTests
{
    [Fact]
    public void SynthesisedOperationId_IsConsumableByDiscoveryAndGenerator()
    {
        var document = Parse("""
        {"openapi":"3.0.1","info":{"title":"No IDs","version":"1"},"paths":{"/get":{"get":{"responses":{"200":{"description":"OK"}}}}}}
        """);
        OpenApiOperationIdentity.EnsureOperationIds(document);
        var store = new OpenApiStore();
        store.SetDocument(document, "{}", "test", "hash", DateTime.UtcNow);
        var inventoryJson = JsonSerializer.Serialize(new DescribeTools(store).ApiListOperations());
        Assert.Contains("Get:/get", inventoryJson);
        var plan = new OpenApiConstraintTestGenerator().Generate(document, "Get:/get");
        Assert.Equal("Get:/get", plan.OperationId);
        Assert.Equal("GET", plan.Method);
        Assert.Equal("/get", plan.Path);
    }

    [Fact]
    public void ConstraintGenerator_UsesInt64Extremes()
    {
        var document = Parse("""
        {"openapi":"3.0.1","info":{"title":"Int64","version":"1"},"paths":{"/pets/{petId}":{"get":{"operationId":"getPet","parameters":[{"name":"petId","in":"path","required":true,"schema":{"type":"integer","format":"int64"}}],"responses":{"200":{"description":"OK"}}}}}}
        """);
        var plan = new OpenApiConstraintTestGenerator().Generate(document, "getPet");
        var values = plan.TestCases.Where(c => c.Category == "numeric-extreme").Select(c => c.Inputs["petId"]).ToArray();
        Assert.Contains(long.MaxValue.ToString(), values);
        Assert.Contains(long.MinValue.ToString(), values);
        Assert.DoesNotContain(int.MaxValue.ToString(), values);
    }

    [Fact]
    public void ConstraintGenerator_TreatsObjectPropertyAsObjectNotString()
    {
        var document = Parse("""
        {"openapi":"3.0.1","info":{"title":"Objects","version":"1"},"paths":{"/pets":{"post":{"operationId":"addPet","requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Pet"}}}},"responses":{"200":{"description":"OK"}}}}},"components":{"schemas":{"Pet":{"type":"object","properties":{"category":{"$ref":"#/components/schemas/Category"}}},"Category":{"type":"object","required":["id"],"properties":{"id":{"type":"integer","format":"int64"},"name":{"type":"string"}}}}}}
        """);
        var plan = new OpenApiConstraintTestGenerator().Generate(document, "addPet");
        Assert.Contains(plan.TestCases, c => c.Category == "object-empty" && c.Target == "requestBody.category");
        Assert.Contains(plan.TestCases, c => c.Category == "object-required-property" && c.Target == "requestBody.category.id");
        Assert.DoesNotContain(plan.TestCases, c => c.Category.StartsWith("string-", StringComparison.Ordinal) && c.Target == "requestBody.category");
    }

    [Fact]
    public void ImportLimit_AcceptsLargeRealWorldContractsWithBoundedHeadroom()
    {
        Assert.True(OpenApiImportLimits.MaxSpecBytes > 12_845_882);
        Assert.True(OpenApiImportLimits.MaxSpecBytes <= 32 * 1024 * 1024);
    }

    [Theory]
    [InlineData("Default", AzureOpenAiCredentialSource.Default)]
    [InlineData("AzureCli", AzureOpenAiCredentialSource.AzureCli)]
    [InlineData("ManagedIdentity", AzureOpenAiCredentialSource.ManagedIdentity)]
    public void AzureCredentialSource_IsExplicitAndValidated(string value, AzureOpenAiCredentialSource expected)
    {
        var options = new AzureOpenAiOptions { CredentialSource = value };
        Assert.Equal(expected, options.GetCredentialSource());
    }

    [Fact]
    public void SessionStore_ClearsAllLoadedContractState()
    {
        var store = new OpenApiStore();
        store.SetDocument(Parse("""{"openapi":"3.0.1","info":{"title":"A","version":"1"},"paths":{}}"""), "raw", "a.json", "hash", DateTime.UtcNow);
        Assert.True(store.HasDocument);
        store.Clear();
        Assert.False(store.HasDocument);
        Assert.Throws<InvalidOperationException>(store.RequireSnapshot);
    }

    private static Microsoft.OpenApi.Models.OpenApiDocument Parse(string json)
    {
        var document = new OpenApiStringReader().Read(json, out var diagnostics);
        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);
        return document;
    }
}
