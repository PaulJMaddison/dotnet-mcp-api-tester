using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/ping", () => Results.Ok(new { ok = true }));

app.MapGet("/v1/widgets/{id}", (int id, bool? includeTags) =>
{
    if (id < 1)
        return Results.BadRequest(new { code = "invalid_id", message = "id must be >= 1" });
    if (id > 9999)
        return Results.NotFound(new { code = "widget_not_found", message = $"widget {id} was not found" });

    return Results.Ok(new
    {
        id,
        name = $"widget-{id}",
        status = "active",
        tags = includeTags == true ? new[] { "demo", "fixture" } : Array.Empty<string>()
    });
});

app.MapPost("/v1/widgets", (WidgetCreateRequest request) =>
{
    if (request.Id is < 1 or > 9999)
        return Results.BadRequest(new { code = "invalid_id", message = "id must be between 1 and 9999" });
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length is < 3 or > 50)
        return Results.BadRequest(new { code = "invalid_name", message = "name length must be between 3 and 50" });
    if (!Regex.IsMatch(request.Name, "^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant))
        return Results.BadRequest(new { code = "invalid_name", message = "name contains unsupported characters" });
    if (request.Status is not ("active" or "inactive"))
        return Results.BadRequest(new { code = "invalid_status", message = "status must be active or inactive" });
    if (request.Tags is { Length: > 3 })
        return Results.BadRequest(new { code = "too_many_tags", message = "at most three tags are allowed" });

    return Results.Ok(new
    {
        request.Id,
        request.Name,
        request.Status,
        tags = request.Tags ?? Array.Empty<string>()
    });
});

app.MapGet("/openapi.json", () => Results.Text(FixtureApiContracts.OpenApiJson, "application/json"));

app.Run();

public sealed record WidgetCreateRequest(int Id, string? Name, string? Status, string[]? Tags);

internal static class FixtureApiContracts
{
    public const string OpenApiJson = """
{
  "openapi": "3.0.1",
  "info": {
    "title": "API Tester Interview Fixture",
    "version": "2.0.0",
    "description": "A deliberately small API with useful boundary conditions for MCP qualification demos."
  },
  "servers": [{ "url": "http://127.0.0.1:5055" }],
  "paths": {
    "/v1/ping": {
      "get": {
        "operationId": "getPing",
        "responses": { "200": { "description": "Healthy response" } }
      }
    },
    "/v1/widgets/{id}": {
      "get": {
        "operationId": "getWidgetById",
        "summary": "Get one widget",
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": { "type": "integer", "format": "int32", "minimum": 1, "maximum": 9999 }
          },
          {
            "name": "includeTags",
            "in": "query",
            "required": false,
            "schema": { "type": "boolean" }
          }
        ],
        "responses": {
          "200": { "description": "Widget returned" },
          "400": { "description": "Invalid id" },
          "404": { "description": "Widget not found" }
        }
      }
    },
    "/v1/widgets": {
      "post": {
        "operationId": "createWidget",
        "summary": "Create a widget",
        "requestBody": {
          "required": true,
          "content": {
            "application/json": {
              "schema": { "$ref": "#/components/schemas/WidgetCreateRequest" }
            }
          }
        },
        "responses": {
          "200": { "description": "Widget created" },
          "400": { "description": "Constraint validation failed" }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "WidgetCreateRequest": {
        "type": "object",
        "required": ["id", "name", "status"],
        "properties": {
          "id": { "type": "integer", "format": "int32", "minimum": 1, "maximum": 9999 },
          "name": { "type": "string", "minLength": 3, "maxLength": 50, "pattern": "^[A-Za-z0-9_-]+$" },
          "status": { "type": "string", "enum": ["active", "inactive"] },
          "tags": {
            "type": "array",
            "maxItems": 3,
            "items": { "type": "string", "maxLength": 20 }
          }
        }
      }
    }
  }
}
""";
}
