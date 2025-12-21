using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed class OpenApiStore
{
    public OpenApiDocument? Document { get; private set; }
    public string? Source { get; private set; }

    public void Set(OpenApiDocument document, string source)
    {
        Document = document;
        Source = source;
    }

    public bool HasDocument => Document is not null;

    public OpenApiDocument RequireDocument()
        => Document ?? throw new InvalidOperationException("No OpenAPI document loaded. Call api.import_openapi first.");
}
