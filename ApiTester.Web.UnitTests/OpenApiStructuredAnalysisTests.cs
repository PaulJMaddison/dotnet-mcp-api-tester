using ApiTester.McpServer.Models;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Readers;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class OpenApiStructuredAnalysisTests
{
    [Fact]
    public void EvidenceBuilder_CreatesOperationSchemaAndSecurityEvidence()
    {
        var chunks = new OpenApiEvidenceBuilder().Build(Spec());

        Assert.Contains(chunks, c => c.Metadata.TryGetValue("EvidenceType", out var type) && type == "operation");
        Assert.Contains(chunks, c => c.Metadata.TryGetValue("EvidenceType", out var type) && type == "schema");
        Assert.Contains(chunks, c => c.Metadata.TryGetValue("EvidenceType", out var type) && type == "security");
    }

    [Fact]
    public void EvidenceBuilder_KeepsCompleteOperationContextTogether()
    {
        var operation = new OpenApiEvidenceBuilder().Build(Spec())
            .Single(c => c.Metadata.TryGetValue("OperationId", out var id) && id == "createPayment");

        Assert.Contains("METHOD: POST", operation.Text);
        Assert.Contains("PATH: /customers/{customerId}/payments", operation.Text);
        Assert.Contains("customerId in=Path required=true", operation.Text);
        Assert.Contains("dryRun in=Query required=false", operation.Text);
        Assert.Contains("X-Correlation-Id in=Header required=false", operation.Text);
        Assert.Contains("REQUEST BODY: required=true", operation.Text);
        Assert.Contains("ref=CreatePaymentRequest", operation.Text);
        Assert.Contains("SECURITY: bearerAuth", operation.Text);
        Assert.Contains("201 Created", operation.Text);
        Assert.Contains("400 Invalid request", operation.Text);
    }

    [Fact]
    public void EvidenceBuilder_SchemaEvidencePreservesConstraintsAndEnums()
    {
        var schema = new OpenApiEvidenceBuilder().Build(Spec())
            .Single(c => c.Metadata.TryGetValue("SchemaName", out var name) && name == "CreatePaymentRequest");

        Assert.Contains("required=[amount,currency]", schema.Text);
        Assert.Contains("minimum=0.01", schema.Text);
        Assert.Contains("maximum=10000", schema.Text);
        Assert.Contains("enum=[GBP,EUR]", schema.Text);
        Assert.Contains("format=email", schema.Text);
        Assert.Contains("maxLength=100", schema.Text);
        Assert.Contains("minItems=1", schema.Text);
        Assert.Contains("maxItems=3", schema.Text);
    }

    [Fact]
    public void EvidenceBuilder_UsesStableIdsAndHashesForSameSpec()
    {
        var builder = new OpenApiEvidenceBuilder();
        var first = builder.Build(Spec());
        var second = builder.Build(Spec());

        Assert.Equal(first.Select(c => c.ChunkId), second.Select(c => c.ChunkId));
        Assert.Equal(first.Select(c => c.ContentHash), second.Select(c => c.ContentHash));
    }

    [Fact]
    public void ConstraintGenerator_UsesPathQueryAndHeaderParameters()
    {
        var plan = GeneratePlan();

        Assert.Contains(plan.Parameters, p => p.Name == "customerId" && p.Location == "Path" && p.Required);
        Assert.Contains(plan.Parameters, p => p.Name == "dryRun" && p.Location == "Query" && !p.Required);
        Assert.Contains(plan.Parameters, p => p.Name == "X-Correlation-Id" && p.Location == "Header");

        Assert.Contains(plan.TestCases, c => c.Category == "required" && c.Target == "Path:customerId");
        Assert.Contains(plan.TestCases, c => c.Category == "format-invalid" && c.Target == "Path:customerId");
        Assert.Contains(plan.TestCases, c => c.Category == "boolean" && c.Target == "Query:dryRun");
        Assert.Contains(plan.TestCases, c => c.Category == "below-min-length" && c.Target == "Header:X-Correlation-Id");
        Assert.Contains(plan.TestCases, c => c.Category == "above-max-length" && c.Target == "Header:X-Correlation-Id");
    }

    [Fact]
    public void ConstraintGenerator_ResolvesReferencedRequestBodySchema()
    {
        var plan = GeneratePlan();

        Assert.Contains(plan.TestCases, c => c.Category == "required-body");
        Assert.Contains(plan.TestCases, c => c.Category == "required-body-property" && c.Target == "requestBody:amount");
        Assert.Contains(plan.TestCases, c => c.Category == "required-body-property" && c.Target == "requestBody:currency");
        Assert.Contains(plan.TestCases, c => c.Category == "below-minimum" && c.Target == "requestBody.amount" && c.Inputs["amount"] == "0.00");
        Assert.Contains(plan.TestCases, c => c.Category == "above-maximum" && c.Target == "requestBody.amount" && c.Inputs["amount"] == "10000.01");
        Assert.Contains(plan.TestCases, c => c.Category == "enum-invalid" && c.Target == "requestBody.currency");
        Assert.Contains(plan.TestCases, c => c.Category == "format-invalid" && c.Target == "requestBody.email");
        Assert.Contains(plan.TestCases, c => c.Category == "below-min-items" && c.Target == "requestBody.tags");
        Assert.Contains(plan.TestCases, c => c.Category == "above-max-items" && c.Target == "requestBody.tags");
    }

    [Fact]
    public void ConstraintGenerator_DerivesAuthenticationAndResponsesWithoutAi()
    {
        var plan = GeneratePlan();

        Assert.True(plan.RequiresAuth);
        Assert.Equal("Created", plan.Responses["201"]);
        Assert.Equal("Invalid request", plan.Responses["400"]);
        Assert.True(plan.TestCases.Count > 20);
    }

    [Fact]
    public void ConstraintGenerator_IsDeterministicForSameContract()
    {
        var first = GeneratePlan();
        var second = GeneratePlan();

        Assert.Equal(first.Parameters, second.Parameters);
        Assert.Equal(first.TestCases, second.TestCases);
    }

    private static GeneratedOperationTestPlan GeneratePlan()
    {
        var reader = new OpenApiStringReader();
        var document = reader.Read(OpenApiJson, out var diagnostics);
        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);
        return new OpenApiConstraintTestGenerator().Generate(document, "createPayment");
    }

    private static OpenApiSpecRecord Spec() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        OrgDefaults.DefaultOrganisationId,
        "Payments API",
        "1.0",
        OpenApiJson,
        "fixture-hash",
        new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc));

    private const string OpenApiJson = """
    {
      "openapi": "3.0.1",
      "info": { "title": "Payments API", "version": "1.0" },
      "paths": {
        "/customers/{customerId}/payments": {
          "parameters": [
            {
              "name": "customerId",
              "in": "path",
              "required": true,
              "schema": { "type": "string", "format": "uuid" }
            }
          ],
          "post": {
            "operationId": "createPayment",
            "summary": "Create a payment",
            "description": "Creates a payment for the selected customer.",
            "parameters": [
              {
                "name": "dryRun",
                "in": "query",
                "required": false,
                "schema": { "type": "boolean" }
              },
              {
                "name": "X-Correlation-Id",
                "in": "header",
                "required": false,
                "schema": { "type": "string", "minLength": 8, "maxLength": 64 }
              }
            ],
            "requestBody": {
              "required": true,
              "content": {
                "application/json": {
                  "schema": { "$ref": "#/components/schemas/CreatePaymentRequest" }
                }
              }
            },
            "security": [{ "bearerAuth": [] }],
            "responses": {
              "201": { "description": "Created" },
              "400": { "description": "Invalid request" }
            }
          }
        }
      },
      "components": {
        "securitySchemes": {
          "bearerAuth": {
            "type": "http",
            "scheme": "bearer",
            "bearerFormat": "JWT"
          }
        },
        "schemas": {
          "CreatePaymentRequest": {
            "type": "object",
            "required": ["amount", "currency"],
            "properties": {
              "amount": { "type": "number", "format": "decimal", "minimum": 0.01, "maximum": 10000 },
              "currency": { "type": "string", "enum": ["GBP", "EUR"] },
              "email": { "type": "string", "format": "email", "maxLength": 100 },
              "tags": { "type": "array", "minItems": 1, "maxItems": 3, "items": { "type": "string" } }
            }
          },
          "Payment": {
            "type": "object",
            "required": ["id"],
            "properties": { "id": { "type": "string", "format": "uuid" } }
          }
        }
      }
    }
    """;
}
