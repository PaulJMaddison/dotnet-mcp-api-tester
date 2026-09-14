using ApiTester.McpServer.Services;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class OpenApiTools
{
    private readonly OpenApiStore _store;
    private readonly ApiRuntimeConfig _runtime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SsrfGuard _ssrfGuard;
    private readonly InMemoryVectorStore _vectors;
    private readonly ILogger<OpenApiTools> _logger;

    public OpenApiTools(
        OpenApiStore store,
        ApiRuntimeConfig runtime,
        IHttpClientFactory httpClientFactory,
        SsrfGuard ssrfGuard,
        InMemoryVectorStore vectors,
        ILogger<OpenApiTools> logger)
    {
        _store = store;
        _runtime = runtime;
        _httpClientFactory = httpClientFactory;
        _ssrfGuard = ssrfGuard;
        _vectors = vectors;
        _logger = logger;
    }

    [McpServerTool, Description("Load an OpenAPI (Swagger) specification from a URL or local file into this process. Replaces the current contract and clears derived RAG state.")]
    public async Task<string> ApiImportOpenApi(string specUrlOrPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(specUrlOrPath))
            throw new ArgumentException("specUrlOrPath is required.", nameof(specUrlOrPath));

        string specText;
        var isRemote = Uri.TryCreate(specUrlOrPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        _logger.LogInformation("Loading OpenAPI spec from {SpecSource} ({Location})", isRemote ? "url" : "path", specUrlOrPath);

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
            if (!fileInfo.Exists)
                throw new FileNotFoundException("OpenAPI file path not found.", specUrlOrPath);

            if (fileInfo.Length > OpenApiImportLimits.MaxSpecBytes)
                throw new InvalidOperationException($"OpenAPI spec exceeds {OpenApiImportLimits.MaxSpecBytes} bytes.");

            specText = await File.ReadAllTextAsync(specUrlOrPath, ct);
        }

        var reader = new OpenApiStringReader();
        var document = reader.Read(specText, out var diagnostics);
        if (document is null)
            throw new InvalidOperationException("OpenAPI document could not be parsed.");

        OpenApiOperationIdentity.EnsureOperationIds(document);

        var loadedUtc = DateTime.UtcNow;
        var specHash = ComputeSha256Hex(specText);
        _store.SetDocument(document, specText, specUrlOrPath, specHash, loadedUtc);
        _vectors.Clear(_store.SessionId);

        var title = document.Info?.Title ?? "(no title)";
        var version = document.Info?.Version ?? "(no version)";
        var paths = document.Paths?.Count ?? 0;

        var sb = new StringBuilder();
        if (diagnostics.Errors.Count > 0)
        {
            sb.AppendLine($"OpenAPI loaded with {diagnostics.Errors.Count} validation issue(s).");
            foreach (var error in diagnostics.Errors.Take(20))
                sb.AppendLine($"- {error.Message}");
            if (diagnostics.Errors.Count > 20)
                sb.AppendLine($"... ({diagnostics.Errors.Count - 20} more)");
        }
        else
        {
            sb.AppendLine("OpenAPI loaded cleanly.");
        }

        sb.AppendLine($"Title: {title}, Version: {version}, Paths: {paths}");
        sb.AppendLine($"Spec hash: {specHash}");
        sb.Append("State: in memory only; restart the MCP server to discard it.");
        return sb.ToString();
    }

    private static async Task<string> ReadBodyCappedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
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

    private static string ComputeSha256Hex(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
