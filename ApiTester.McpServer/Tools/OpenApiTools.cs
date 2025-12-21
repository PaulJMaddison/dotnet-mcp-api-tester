using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class OpenApiTools
{
    private readonly OpenApiStore _store;

    public OpenApiTools(OpenApiStore store)
    {
        _store = store;
    }

    [McpServerTool, Description("Import an OpenAPI (Swagger) specification from a URL or local file path.")]
    public async Task<string> ApiImportOpenApi(string specUrlOrPath)
    {
        if (string.IsNullOrWhiteSpace(specUrlOrPath))
            throw new ArgumentException("specUrlOrPath is required", nameof(specUrlOrPath));

        string raw;

        if (Uri.TryCreate(specUrlOrPath, UriKind.Absolute, out var uri))
        {
            using var http = new HttpClient();
            raw = await http.GetStringAsync(uri);
        }
        else
        {
            raw = await File.ReadAllTextAsync(specUrlOrPath);
        }

        // Use Stream reader for compatibility.
        var bytes = Encoding.UTF8.GetBytes(raw);
        using var ms = new MemoryStream(bytes);

        var reader = new OpenApiStreamReader();
        OpenApiDocument document = reader.Read(ms, out var diagnostic);

        if (diagnostic.Errors is { Count: > 0 })
        {
            var errors = string.Join("\n", diagnostic.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"OpenAPI parse errors:\n{errors}");
        }

        _store.Set(document, specUrlOrPath);

        return $"OpenAPI loaded. Title: {document.Info?.Title ?? "(no title)"}, Version: {document.Info?.Version ?? "(no version)"}, Paths: {document.Paths.Count}";
    }

    [McpServerTool, Description("List all operations defined in the currently loaded OpenAPI specification.")]
    public string ApiListOperations()
    {
        var doc = _store.RequireDocument();

        var ops = new List<object>();

        foreach (var path in doc.Paths)
        {
            foreach (var op in path.Value.Operations)
            {
                var operation = op.Value;

                // operation.OperationId can be null in real-world specs
                var opId = string.IsNullOrWhiteSpace(operation.OperationId)
                    ? $"{op.Key}:{path.Key}"
                    : operation.OperationId;

                ops.Add(new
                {
                    operationId = opId,
                    method = op.Key.ToString(),
                    path = path.Key,
                    summary = operation.Summary ?? "",
                    description = operation.Description ?? "",
                    requiresAuth = operation.Security is { Count: > 0 }
                });
            }
        }

        return JsonSerializer.Serialize(ops, new JsonSerializerOptions { WriteIndented = true });
    }
}
