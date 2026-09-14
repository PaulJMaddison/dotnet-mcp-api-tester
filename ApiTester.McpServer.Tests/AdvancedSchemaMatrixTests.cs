using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class AdvancedSchemaMatrixTests
{
    private readonly OpenApiConstraintTestGenerator _generator = new();

    [Fact]
    public void RecursesIntoNestedReferencedBodyConstraints()
    {
        var doc = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"Nested","version":"1"},
          "paths":{"/orders":{"post":{"operationId":"createOrder","requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Order"}}}},"responses":{"201":{"description":"Created"}}}}},
          "components":{"schemas":{
            "Order":{"type":"object","required":["customer"],"properties":{"customer":{"$ref":"#/components/schemas/Customer"}}},
            "Customer":{"type":"object","required":["contact"],"properties":{"contact":{"$ref":"#/components/schemas/Contact"}}},
            "Contact":{"type":"object","required":["email"],"properties":{"email":{"type":"string","format":"email"},"age":{"type":"integer","minimum":18,"maximum":120}}}
          }}
        }
        """);

        var plan = _generator.Generate(doc, "createOrder");

        Assert.Contains(plan.TestCases, c => c.Category == "required-body-property" && c.Target == "requestBody:customer");
        Assert.Contains(plan.TestCases, c => c.Category == "object-required-property" && c.Target == "requestBody.customer.contact");
        Assert.Contains(plan.TestCases, c => c.Category == "format-invalid" && c.Target == "requestBody.customer.contact.email");
        Assert.Contains(plan.TestCases, c => c.Category == "below-minimum" && c.Target == "requestBody.customer.contact.age");
        Assert.Contains(plan.TestCases, c => c.Category == "above-maximum" && c.Target == "requestBody.customer.contact.age");
    }

    [Fact]
    public void RootArrayRequestBodyGetsCollectionBoundaryCases()
    {
        var doc = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"Arrays","version":"1"},
          "paths":{"/bulk":{"post":{"operationId":"bulkCreate","requestBody":{"required":true,"content":{"application/json":{"schema":{"type":"array","minItems":1,"maxItems":3,"items":{"$ref":"#/components/schemas/Item"}}}}},"responses":{"202":{"description":"Accepted"}}}}},
          "components":{"schemas":{"Item":{"type":"object","required":["id"],"properties":{"id":{"type":"integer"}}}}}
        }
        """);

        var plan = _generator.Generate(doc, "bulkCreate");

        Assert.Contains(plan.TestCases, c => c.Category == "required-body" && c.Target == "requestBody");
        Assert.Contains(plan.TestCases, c => c.Category == "array-empty" && c.Target == "requestBody");
        Assert.Contains(plan.TestCases, c => c.Category == "below-min-items" && c.Target == "requestBody");
        Assert.Contains(plan.TestCases, c => c.Category == "min-items" && c.Target == "requestBody");
        Assert.Contains(plan.TestCases, c => c.Category == "max-items" && c.Target == "requestBody");
        Assert.Contains(plan.TestCases, c => c.Category == "above-max-items" && c.Target == "requestBody");
    }

    [Fact]
    public void AllOfComposedBodyMergesInheritedRequiredFieldsAndConstraints()
    {
        var doc = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"AllOf","version":"1"},
          "paths":{"/employees":{"post":{"operationId":"createEmployee","requestBody":{"required":true,"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Employee"}}}},"responses":{"201":{"description":"Created"}}}}},
          "components":{"schemas":{
            "Person":{"type":"object","required":["name"],"properties":{"name":{"type":"string","minLength":2}}},
            "Employee":{"allOf":[{"$ref":"#/components/schemas/Person"},{"type":"object","required":["employeeId"],"properties":{"employeeId":{"type":"integer","minimum":1}}}]}
          }}
        }
        """);

        var plan = _generator.Generate(doc, "createEmployee");

        Assert.Contains(plan.TestCases, c => c.Category == "required-body-property" && c.Target == "requestBody:name");
        Assert.Contains(plan.TestCases, c => c.Category == "below-min-length" && c.Target == "requestBody.name");
        Assert.Contains(plan.TestCases, c => c.Category == "required-body-property" && c.Target == "requestBody:employeeId");
        Assert.Contains(plan.TestCases, c => c.Category == "below-minimum" && c.Target == "requestBody.employeeId");
    }

    [Fact]
    public void OneOfBodyProducesVariantSpecificCoverageRatherThanTreatingItAsString()
    {
        var doc = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"OneOf","version":"1"},
          "paths":{"/payments":{"post":{"operationId":"pay","requestBody":{"required":true,"content":{"application/json":{"schema":{"oneOf":[
            {"type":"object","required":["cardNumber"],"properties":{"cardNumber":{"type":"string","minLength":12}}},
            {"type":"object","required":["accountId"],"properties":{"accountId":{"type":"integer","minimum":1}}}
          ]}}}},"responses":{"200":{"description":"OK"}}}}}
        }
        """);

        var plan = _generator.Generate(doc, "pay");

        Assert.Contains(plan.TestCases, c => c.Target.Contains("cardNumber", StringComparison.Ordinal));
        Assert.Contains(plan.TestCases, c => c.Target.Contains("accountId", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.TestCases, c => c.Target == "requestBody" && c.Category.StartsWith("string-", StringComparison.Ordinal));
    }

    private static Microsoft.OpenApi.Models.OpenApiDocument Parse(string json)
    {
        var doc = new OpenApiStringReader().Read(json, out var diagnostics);
        Assert.NotNull(doc);
        Assert.Empty(diagnostics.Errors);
        return doc;
    }
}
