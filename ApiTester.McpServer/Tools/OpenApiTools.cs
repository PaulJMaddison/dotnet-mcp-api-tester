using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class OpenApiTools
{
    private readonly OpenApiStore _store;
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenApiTools(OpenApiStore store, IHttpClientFactory httpClientFactory)
    {
        _store = store;
        _httpClientFactory = httpClientFactory;
    }

    [McpServerTool, Description("Import an OpenAPI (Swagger) specification from a URL or local file path.")]
    public async Task<string> ApiImportOpenApi(string specUrlOrPath)
    {
        if (string.IsNullOrWhiteSpace(specUrlOrPath))
            throw new ArgumentException("specUrlOrPath is required.", nameof(specUrlOrPath));

        string specText;

        if (Uri.TryCreate(specUrlOrPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var client = _httpClientFactory.CreateClient();
            specText = await client.GetStringAsync(uri);
        }
        else
        {
            specText = await File.ReadAllTextAsync(specUrlOrPath);
        }

        var reader = new OpenApiStringReader();
        var doc = reader.Read(specText, out var diag);

        if (doc is null)
            throw new InvalidOperationException("OpenAPI document could not be parsed (doc was null).");

        // IMPORTANT: load it even if there are errors, so the tool still works on messy specs
        _store.SetDocument(doc);

        var sb = new StringBuilder();

        if (diag.Errors.Count > 0)
        {
            sb.AppendLine($"OpenAPI loaded WITH {diag.Errors.Count} validation issue(s). Continuing anyway.");
            foreach (var e in diag.Errors.Take(20))
                sb.AppendLine($"- {e.Message}");

            if (diag.Errors.Count > 20)
                sb.AppendLine($"... ({diag.Errors.Count - 20} more)");
        }
        else
        {
            sb.AppendLine("OpenAPI loaded cleanly.");
        }

        var title = doc.Info?.Title ?? "(no title)";
        var version = doc.Info?.Version ?? "(no version)";
        var paths = doc.Paths?.Count ?? 0;

        sb.AppendLine($"Title: {title}, Version: {version}, Paths: {paths}");

        return sb.ToString().Trim();
    }
}
