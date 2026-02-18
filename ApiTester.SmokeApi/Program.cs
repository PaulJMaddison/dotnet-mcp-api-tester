var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/v1/ping", () => Results.Ok(new { ok = true }));

app.MapGet("/v1/widgets/{id}", (int id) => Results.Ok(new
{
    id,
    name = $"widget-{id}",
    status = "active",
    tags = new[] { "e2e", "fixture" }
}));

app.MapPost("/v1/widgets", (WidgetCreateRequest request) =>
{
    var id = request.Id > 0 ? request.Id : 4242;
    return Results.Ok(new
    {
        id,
        name = string.IsNullOrWhiteSpace(request.Name) ? $"widget-{id}" : request.Name,
        status = string.IsNullOrWhiteSpace(request.Status) ? "active" : request.Status,
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
  "info": { "title": "Fixture API", "version": "1.0.0" },
  "servers": [{ "url": "http://fixtureapi:8080" }],
  "paths": {
    "/v1/ping": {
      "get": {
        "operationId": "getPing",
        "responses": { "200": { "description": "OK" } }
      }
    },
    "/v1/widgets/{id}": {
      "get": {
        "operationId": "getWidgetById",
        "parameters": [{ "name": "id", "in": "path", "required": true, "schema": { "type": "integer", "format": "int32" } }],
        "responses": { "200": { "description": "OK" } }
      }
    },
    "/v1/widgets": {
      "post": {
        "operationId": "createWidget",
        "requestBody": {
          "required": true,
          "content": { "application/json": { "schema": { "type": "object" } } }
        },
        "responses": { "200": { "description": "OK" } }
      }
    }
  }
}
""";
}
