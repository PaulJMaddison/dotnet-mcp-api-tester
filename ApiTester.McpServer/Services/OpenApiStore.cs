using Microsoft.OpenApi.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ApiTester.McpServer.Services;

public sealed class OpenApiStore
{
    private readonly object _gate = new();

    public OpenApiDocument? Document { get; private set; }
    public string? Source { get; private set; }

    public void SetDocument(OpenApiDocument doc)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc));
        lock (_gate) Document = doc;
    }

    public OpenApiDocument RequireDocument()
    {
        lock (_gate)
        {
            return Document ?? throw new InvalidOperationException("No OpenAPI document loaded. Call api_import_open_api first.");
        }
    }
    public bool HasDocument => Document is not null;

}
