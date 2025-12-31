using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class OpenApiTools
{
    private readonly OpenApiStore _store;
    private readonly ApiRuntimeConfig _runtime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SsrfGuard _ssrfGuard;
    private readonly ILogger<OpenApiTools> _logger;

    public OpenApiTools(
        OpenApiStore store,
        ApiRuntimeConfig runtime,
        IHttpClientFactory httpClientFactory,
        SsrfGuard ssrfGuard,
        ILogger<OpenApiTools> logger)
    {
        _store = store;
        _runtime = runtime;
        _httpClientFactory = httpClientFactory;
        _ssrfGuard = ssrfGuard;
        _logger = logger;
    }

    [McpServerTool, Description("Import an OpenAPI (Swagger) specification from a URL or local file path.")]
    public async Task<string> ApiImportOpenApi(string specUrlOrPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(specUrlOrPath))
            throw new ArgumentException("specUrlOrPath is required.", nameof(specUrlOrPath));

        string specText;

        var isRemote = Uri.TryCreate(specUrlOrPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        _logger.LogInformation("Importing OpenAPI spec from {SpecSource} ({Location})", isRemote ? "url" : "path", specUrlOrPath);

        if (isRemote)
        {
            var (allowed, reason) = await _ssrfGuard.CheckAsync(
                uri!,
                _runtime.Policy.BlockLocalhost,
                _runtime.Policy.BlockPrivateNetworks,
                ct);

            if (!allowed)
                throw new InvalidOperationException($"Blocked OpenAPI URL: {reason}");

            var client = _httpClientFactory.CreateClient();
            client.Timeout = _runtime.Policy.Timeout;

            using var response = await client.GetAsync(uri!, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > OpenApiImportLimits.MaxSpecBytes)
                throw new InvalidOperationException($"OpenAPI spec exceeds {OpenApiImportLimits.MaxSpecBytes} bytes.");

            specText = await ReadBodyCappedAsync(response.Content, OpenApiImportLimits.MaxSpecBytes, ct);
        }
        else
        {
            var fileInfo = new FileInfo(specUrlOrPath);
            if (fileInfo.Length > OpenApiImportLimits.MaxSpecBytes)
                throw new InvalidOperationException($"OpenAPI spec exceeds {OpenApiImportLimits.MaxSpecBytes} bytes.");

            specText = await File.ReadAllTextAsync(specUrlOrPath, ct);
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

        _logger.LogInformation(
            "OpenAPI spec loaded {Title} {Version} with {PathCount} paths and {ErrorCount} validation error(s)",
            title,
            version,
            paths,
            diag.Errors.Count);

        sb.AppendLine($"Title: {title}, Version: {version}, Paths: {paths}");

        return sb.ToString().Trim();
    }

    private static async Task<string> ReadBodyCappedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();

        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (read <= 0) break;

            var remaining = maxBytes - total;
            if (remaining <= 0)
                throw new InvalidOperationException($"OpenAPI spec exceeds {maxBytes} bytes.");

            var toWrite = Math.Min(read, remaining);
            ms.Write(buffer, 0, toWrite);
            total += toWrite;

            if (toWrite < read)
                throw new InvalidOperationException($"OpenAPI spec exceeds {maxBytes} bytes.");
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
