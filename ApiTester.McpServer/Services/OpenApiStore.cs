using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed record OpenApiSnapshot(
    Guid SessionId,
    Guid SourceId,
    OpenApiDocument Document,
    string RawSpec,
    string? Source,
    string SpecHash,
    DateTime LoadedUtc);

public sealed class OpenApiStore
{
    private readonly object _gate = new();
    private OpenApiSnapshot? _snapshot;

    public Guid SessionId { get; } = Guid.NewGuid();

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
        OpenApiDocument document,
        string rawSpec,
        string? source,
        string specHash,
        DateTime loadedUtc)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(rawSpec))
            throw new ArgumentException("rawSpec is required.", nameof(rawSpec));
        if (string.IsNullOrWhiteSpace(specHash))
            throw new ArgumentException("specHash is required.", nameof(specHash));

        lock (_gate)
        {
            _snapshot = new OpenApiSnapshot(
                SessionId,
                Guid.NewGuid(),
                document,
                rawSpec,
                source,
                specHash,
                loadedUtc);
        }
    }

    public OpenApiDocument RequireDocument() => RequireSnapshot().Document;

    public OpenApiSnapshot RequireSnapshot()
    {
        lock (_gate)
        {
            return _snapshot
                ?? throw new InvalidOperationException("No OpenAPI document loaded. Call api_import_open_api first.");
        }
    }

    public void Clear()
    {
        lock (_gate)
            _snapshot = null;
    }
}
