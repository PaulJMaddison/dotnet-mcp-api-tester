using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class ConstraintGeneratorMatrixTests
{
    private readonly OpenApiConstraintTestGenerator _generator = new();

    [Fact]
    public void CoversAllParameterLocationsAndCoreTypes()
    {
        var document = Parse("""
        {
          "openapi":"3.0.1",
          "info":{"title":"Matrix","version":"1"},
          "paths":{"/items/{id}":{"get":{"operationId":"getItem","parameters":[
            {"name":"id","in":"path","required":true,"schema":{"type":"integer","format":"int32"}},
            {"name":"search","in":"query","required":false,"schema":{"type":"string","nullable":true}},
            {"name":"enabled","in":"query","schema":{"type":"boolean"}},
            {"name":"ratio","in":"query","schema":{"type":"number","minimum":0.5,"maximum":1.5}},
            {"name":"tags","in":"query","schema":{"type":"array","minItems":1,"maxItems":2,"items":{"type":"string"}}},
            {"name":"X-Mode","in":"header","required":true,"schema":{"type":"string","enum":["fast","safe"]}},
            {"name":"sid","in":"cookie","schema":{"type":"string","format":"uuid"}}
          ],"responses":{"200":{"description":"OK"}}}}}
        }
        """);

        var plan = _generator.Generate(document, "getItem");

        Assert.Contains(plan.Parameters, p => p.Name == "id" && p.Location == "Path" && p.Type == "integer");
        Assert.Contains(plan.Parameters, p => p.Name == "search" && p.Location == "Query" && p.Nullable);
        Assert.Contains(plan.Parameters, p => p.Name == "X-Mode" && p.Location == "Header");
        Assert.Contains(plan.Parameters, p => p.Name == "sid" && p.Location == "Cookie");

        Assert.Contains(plan.TestCases, c => c.Category == "required" && c.Target.Contains("id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.TestCases, c => c.Category == "optional" && c.Target.Contains("search", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.TestCases, c => c.Category == "nullable" && c.Target.Contains("search", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.TestCases, c => c.Category == "boolean" && c.Inputs.TryGetValue("enabled", out var value) && value == "true");
        Assert.Contains(plan.TestCases, c => c.Category == "wrong-type" && c.Inputs.ContainsKey("enabled"));
        Assert.Contains(plan.TestCases, c => c.Category == "below-minimum" && c.Inputs.ContainsKey("ratio"));
        Assert.Contains(plan.TestCases, c => c.Category == "above-maximum" && c.Inputs.ContainsKey("ratio"));
        Assert.Contains(plan.TestCases, c => c.Category == "below-min-items" && c.Inputs.ContainsKey("tags"));
        Assert.Contains(plan.TestCases, c => c.Category == "above-max-items" && c.Inputs.ContainsKey("tags"));
        Assert.Contains(plan.TestCases, c => c.Category == "enum-valid" && c.Inputs.TryGetValue("X-Mode", out var value) && value == "fast");
        Assert.Contains(plan.TestCases, c => c.Category == "enum-invalid" && c.Inputs.ContainsKey("X-Mode"));
        Assert.Contains(plan.TestCases, c => c.Category == "format-valid" && c.Inputs.TryGetValue("sid", out var value) && value == "11111111-2222-3333-4444-555555555555");
        Assert.Contains(plan.TestCases, c => c.Category == "format-invalid" && c.Inputs.TryGetValue("sid", out var value) && value == "not-a-uuid");
    }

    [Fact]
    public void CoversStringLengthsPatternsAndKnownFormats()
    {
        var document = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"Strings","version":"1"},
          "paths":{"/validate":{"get":{"operationId":"validate","parameters":[
            {"name":"code","in":"query","schema":{"type":"string","minLength":2,"maxLength":4,"pattern":"^[A-Z]+$"}},
            {"name":"email","in":"query","schema":{"type":"string","format":"email"}},
            {"name":"date","in":"query","schema":{"type":"string","format":"date"}},
            {"name":"when","in":"query","schema":{"type":"string","format":"date-time"}},
            {"name":"link","in":"query","schema":{"type":"string","format":"uri"}}
          ],"responses":{"200":{"description":"OK"}}}}}
        }
        """);

        var plan = _generator.Generate(document, "validate");

        foreach (var category in new[] { "below-min-length", "min-length", "above-min-length", "below-max-length", "max-length", "above-max-length", "pattern-invalid" })
            Assert.Contains(plan.TestCases, c => c.Category == category && c.Target.Contains("code", StringComparison.OrdinalIgnoreCase));

        AssertFormatCases(plan, "email", "person@example.com", "not-an-email");
        AssertFormatCases(plan, "date", "2026-09-14", "2026-02-31");
        AssertFormatCases(plan, "when", "2026-09-14T14:00:00Z", "not-a-date-time");
        AssertFormatCases(plan, "link", "https://example.com/resource", "://bad-uri");
        Assert.Contains(plan.TestCases, c => c.Category == "string-unicode" && c.Inputs.ContainsKey("code"));
        Assert.Contains(plan.TestCases, c => c.Category == "string-special" && c.Inputs.ContainsKey("code"));
    }

    [Fact]
    public void OperationParameterOverridesPathLevelDefinition()
    {
        var document = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"Override","version":"1"},
          "paths":{"/items":{"parameters":[{"name":"q","in":"query","schema":{"type":"string","minLength":8}}],
          "get":{"operationId":"search","parameters":[{"name":"q","in":"query","schema":{"type":"integer","maximum":10}}],"responses":{"200":{"description":"OK"}}}}}
        }
        """);

        var plan = _generator.Generate(document, "search");
        var q = Assert.Single(plan.Parameters.Where(p => p.Name == "q"));

        Assert.Equal("integer", q.Type);
        Assert.Equal(10m, q.Maximum);
        Assert.Contains(plan.TestCases, c => c.Category == "maximum" && c.Inputs.TryGetValue("q", out var value) && value == "10");
        Assert.DoesNotContain(plan.TestCases, c => c.Category == "min-length" && c.Inputs.ContainsKey("q"));
    }

    [Fact]
    public void CoversRequiredBodyReferencedObjectsNestedRequiredFieldsAndMultipleContentTypes()
    {
        var document = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"Body","version":"1"},
          "paths":{"/orders":{"post":{"operationId":"createOrder","requestBody":{"required":true,"content":{
            "application/json":{"schema":{"$ref":"#/components/schemas/Order"}},
            "application/problem+json":{"schema":{"$ref":"#/components/schemas/Order"}}
          }},"responses":{"201":{"description":"Created"},"400":{"description":"Invalid"}}}}},
          "components":{"schemas":{
            "Order":{"type":"object","required":["name","customer"],"properties":{
              "name":{"type":"string","minLength":1},
              "quantity":{"type":"integer","minimum":1,"maximum":100},
              "customer":{"$ref":"#/components/schemas/Customer"}
            }},
            "Customer":{"type":"object","required":["id"],"properties":{"id":{"type":"integer","format":"int64"},"email":{"type":"string","format":"email"}}}
          }}
        }
        """);

        var plan = _generator.Generate(document, "createOrder");

        Assert.Contains(plan.TestCases, c => c.Category == "required-body" && c.Target == "requestBody");
        Assert.Contains(plan.TestCases, c => c.Category == "required-body-property" && c.Target == "requestBody:name");
        Assert.Contains(plan.TestCases, c => c.Category == "minimum" && c.Target == "requestBody.quantity");
        Assert.Contains(plan.TestCases, c => c.Category == "maximum" && c.Target == "requestBody.quantity");
        Assert.Contains(plan.TestCases, c => c.Category == "object-empty" && c.Target == "requestBody.customer");
        Assert.Contains(plan.TestCases, c => c.Category == "object-required-property" && c.Target == "requestBody.customer.id");
        Assert.Contains(plan.TestCases, c => c.Description.Contains("application/json", StringComparison.Ordinal));
        Assert.Contains(plan.TestCases, c => c.Description.Contains("application/problem+json", StringComparison.Ordinal));
        Assert.Equal("Created", plan.Responses["201"]);
        Assert.Equal("Invalid", plan.Responses["400"]);
    }

    [Fact]
    public void OperationSecurityEmptyArrayOverridesGlobalSecurity()
    {
        var document = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"Security","version":"1"},
          "security":[{"bearerAuth":[]}],
          "paths":{
            "/public":{"get":{"operationId":"publicGet","security":[],"responses":{"200":{"description":"OK"}}}},
            "/secure":{"get":{"operationId":"secureGet","responses":{"200":{"description":"OK"}}}}
          },
          "components":{"securitySchemes":{"bearerAuth":{"type":"http","scheme":"bearer"}}}
        }
        """);

        Assert.False(_generator.Generate(document, "publicGet").RequiresAuth);
        Assert.True(_generator.Generate(document, "secureGet").RequiresAuth);
    }

    [Fact]
    public void FlagsPathTemplateWithoutDeclaredPathParameter()
    {
        var document = Parse("""
        {"openapi":"3.0.1","info":{"title":"Broken","version":"1"},"paths":{"/items/{id}":{"get":{"operationId":"broken","responses":{"200":{"description":"OK"}}}}}}
        """);

        var plan = _generator.Generate(document, "broken");
        Assert.Contains(plan.TestCases, c => c.Category == "contract-consistency" && c.Target == "/items/{id}");
    }

    [Fact]
    public void Swagger2MissingOperationIdStillProducesUsableIdentityAndCases()
    {
        var document = Parse("""
        {
          "swagger":"2.0","info":{"title":"Legacy","version":"1"},"basePath":"/api",
          "paths":{"/pets/{id}":{"get":{"parameters":[{"name":"id","in":"path","required":true,"type":"integer","format":"int64"}],"responses":{"200":{"description":"OK"}}}}}
        }
        """);

        OpenApiOperationIdentity.EnsureOperationIds(document);
        var plan = _generator.Generate(document, "Get:/pets/{id}");

        Assert.Equal("GET", plan.Method);
        Assert.Contains(plan.TestCases, c => c.Category == "numeric-extreme" && c.Inputs.TryGetValue("id", out var value) && value == long.MaxValue.ToString());
    }

    private static void AssertFormatCases(GeneratedOperationTestPlan plan, string name, string valid, string invalid)
    {
        Assert.Contains(plan.TestCases, c => c.Category == "format-valid" && c.Inputs.TryGetValue(name, out var value) && value == valid);
        Assert.Contains(plan.TestCases, c => c.Category == "format-invalid" && c.Inputs.TryGetValue(name, out var value) && value == invalid);
    }

    private static Microsoft.OpenApi.Models.OpenApiDocument Parse(string text)
    {
        var document = new OpenApiStringReader().Read(text, out var diagnostics);
        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);
        OpenApiSecuritySemantics.PreserveExplicitOverrides(document, text);
        return document;
    }
}
