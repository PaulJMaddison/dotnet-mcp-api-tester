using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class OpenApiTools
{
    private readonly OpenApiStore _store;
    private readonly ApiRuntimeConfig _runtime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SsrfGuard _ssrfGuard;
    private readonly OpenApiEvidenceBuilder _evidenceBuilder;
    private readonly RagRuntime _rag;
    private readonly InMemoryVectorStore _vectors;
    private readonly ILogger<OpenApiTools> _logger;
    private readonly QualificationTelemetry? _telemetry;

    public OpenApiTools(
        OpenApiStore store,
        ApiRuntimeConfig runtime,
        IHttpClientFactory httpClientFactory,
        SsrfGuard ssrfGuard,
        OpenApiEvidenceBuilder evidenceBuilder,
        RagRuntime rag,
        InMemoryVectorStore vectors,
        ILogger<OpenApiTools> logger,
        QualificationTelemetry? telemetry = null)
    {
        _store = store;
        _runtime = runtime;
        _httpClientFactory = httpClientFactory;
        _ssrfGuard = ssrfGuard;
        _evidenceBuilder = evidenceBuilder;
        _rag = rag;
        _vectors = vectors;
        _logger = logger;
        _telemetry = telemetry;
    }

    [McpServerTool, Description("Load an OpenAPI/Swagger definition from a URL or local file. Parses it, builds semantic operation/schema/security evidence and replaces the in-memory vector index atomically.")]
    public async Task<object> ApiLoadOpenApi(string specUrlOrPath, CancellationToken ct = default)
    {
        var started = Stopwatch.StartNew();
        _telemetry?.Emit("mcp.tool.start", new { toolName = "api_load_open_api", source = specUrlOrPath });
        if (string.IsNullOrWhiteSpace(specUrlOrPath))
            throw new ArgumentException("specUrlOrPath is required.", nameof(specUrlOrPath));

        _telemetry?.Emit("openapi.load.start", new { source = specUrlOrPath.Trim() });
        var specText = await ReadSpecAsync(specUrlOrPath.Trim(), ct).ConfigureAwait(false);
        _telemetry?.Emit("openapi.fetch.completed", new { source = specUrlOrPath.Trim(), bytes = Encoding.UTF8.GetByteCount(specText) });
        var reader = new OpenApiStringReader();
        var document = reader.Read(specText, out var diagnostics)
            ?? throw new InvalidOperationException("OpenAPI document could not be parsed.");
        _telemetry?.Emit("openapi.parse.completed", new { validationIssues = diagnostics.Errors.Count });

        OpenApiSecuritySemantics.PreserveExplicitOverrides(document, specText);
        OpenApiOperationIdentity.EnsureOperationIds(document);

        var scopeId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var loadedUtc = DateTime.UtcNow;
        var title = document.Info?.Title ?? "(no title)";
        var version = document.Info?.Version ?? "(no version)";
        var specHash = Hash(specText);

        var chunks = _evidenceBuilder.Build(document, scopeId, sourceId, title, version, loadedUtc);
        _telemetry?.Emit("evidence.build.completed", new { scopeId, chunkCount = chunks.Count });
        if (chunks.Count == 0)
            throw new InvalidOperationException("The OpenAPI document produced no operation, schema or security evidence to index.");

        // Index under a new scope before publishing it as current. If embeddings fail,
        // the previous loaded API remains usable and no partial new index becomes active.
        await _rag.Indexer.IndexAsync(chunks, ct).ConfigureAwait(false);
        _telemetry?.Emit("embedding.index.completed", new { scopeId, vectorCount = chunks.Count, durationMs = started.ElapsedMilliseconds });

        var previousScope = _store.Current?.ScopeId;
        _store.SetDocument(scopeId, sourceId, document, specUrlOrPath, specHash, loadedUtc);
        if (previousScope is Guid oldScope && oldScope != scopeId)
            _vectors.Clear(oldScope);

        var operationChunks = chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("operation", StringComparison.OrdinalIgnoreCase));
        var schemaChunks = chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("schema", StringComparison.OrdinalIgnoreCase));
        var securityChunks = chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("security", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("Loaded and indexed OpenAPI {Title} {Version}: {ChunkCount} semantic chunks", title, version, chunks.Count);
        _telemetry?.Emit("mcp.tool.end", new { toolName = "api_load_open_api", scopeId, durationMs = started.ElapsedMilliseconds, success = true });

        return new
        {
            ok = true,
            title,
            version,
            paths = document.Paths?.Count ?? 0,
            operations = operationChunks,
            schemas = schemaChunks,
            securitySchemes = securityChunks,
            indexedChunks = chunks.Count,
            validationIssues = diagnostics.Errors.Count,
            specHash,
            persistence = "none"
        };
    }

    private async Task<string> ReadSpecAsync(string source, CancellationToken ct)
    {
        var isRemote = Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        if (!isRemote)
        {
            var file = new FileInfo(source);
            if (!file.Exists) throw new FileNotFoundException("OpenAPI file path not found.", source);
            if (file.Length > OpenApiImportLimits.MaxSpecBytes)
                throw new InvalidOperationException($"OpenAPI spec exceeds {OpenApiImportLimits.MaxSpecBytes} bytes.");
            return await File.ReadAllTextAsync(source, ct).ConfigureAwait(false);
        }

        var (allowed, reason) = await _ssrfGuard.CheckAsync(
            uri!,
            _runtime.Policy.BlockLocalhost,
            _runtime.Policy.BlockPrivateNetworks,
            ct).ConfigureAwait(false);
        if (!allowed) throw new InvalidOperationException($"Blocked OpenAPI URL: {reason}");

        _telemetry?.Emit("openapi.fetch.start", new { httpMethod = "GET", host = uri!.Host, path = uri.AbsolutePath, policyDecision = "allowed" });

        var client = _httpClientFactory.CreateClient();
        client.Timeout = _runtime.Policy.Timeout;
        using var response = await client.GetAsync(uri!, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > OpenApiImportLimits.MaxSpecBytes)
            throw new InvalidOperationException($"OpenAPI spec exceeds {OpenApiImportLimits.MaxSpecBytes} bytes.");

        return await ReadBodyCappedAsync(response.Content, OpenApiImportLimits.MaxSpecBytes, ct).ConfigureAwait(false);
    }

    private static async Task<string> ReadBodyCappedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (read == 0) break;
            if (total + read > maxBytes) throw new InvalidOperationException($"OpenAPI spec exceeds {maxBytes} bytes.");
            output.Write(buffer, 0, read);
            total += read;
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
