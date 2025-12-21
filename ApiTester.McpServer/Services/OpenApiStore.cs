using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed class OpenApiStore
{
    private readonly object _gate = new();

    public OpenApiDocument? Document { get; private set; }
    public string? Source { get; private set; }

    public void SetDocument(OpenApiDocument doc, string? source = null)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc));

        lock (_gate)
        {
            Document = doc;
            Source = source;
        }
    }

    public OpenApiDocument RequireDocument()
    {
        lock (_gate)
        {
            return Document ?? throw new InvalidOperationException("No OpenAPI document loaded. Call api_import_open_api first.");
        }
    }

    public bool HasDocument
    {
        get { lock (_gate) return Document is not null; }
    }
}
