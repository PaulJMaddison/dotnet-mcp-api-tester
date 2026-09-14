using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed record OpenApiSnapshot(
    Guid ScopeId,
    Guid SourceId,
    OpenApiDocument Document,
    string? Source,
    string SpecHash,
    DateTime LoadedUtc);

public sealed class OpenApiStore
{
    private readonly object _gate = new();
    private OpenApiSnapshot? _snapshot;

    public OpenApiSnapshot? Current
    {
        get { lock (_gate) return _snapshot; }
    }

    public OpenApiDocument? Document
    {
        get { lock (_gate) return _snapshot?.Document; }
    }

    public string? Source
    {
        get { lock (_gate) return _snapshot?.Source; }
    }

    public bool HasDocument
    {
        get { lock (_gate) return _snapshot is not null; }
    }

    public void SetDocument(
        Guid scopeId,
        Guid sourceId,
        OpenApiDocument document,
        string? source,
        string specHash,
        DateTime loadedUtc)
    {
        if (scopeId == Guid.Empty) throw new ArgumentException("scopeId is required.", nameof(scopeId));
        if (sourceId == Guid.Empty) throw new ArgumentException("sourceId is required.", nameof(sourceId));
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(specHash)) throw new ArgumentException("specHash is required.", nameof(specHash));

        lock (_gate)
            _snapshot = new OpenApiSnapshot(scopeId, sourceId, document, source, specHash, loadedUtc);
    }

    public OpenApiDocument RequireDocument() => RequireSnapshot().Document;

    public OpenApiSnapshot RequireSnapshot()
    {
        lock (_gate)
        {
            return _snapshot
                ?? throw new InvalidOperationException("No OpenAPI document loaded. Call api_load_open_api first.");
        }
    }

    public void Clear()
    {
        lock (_gate)
            _snapshot = null;
    }
}
