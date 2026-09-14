using System.ComponentModel;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class RagTools
{
    private readonly RagRuntime _rag;
    private readonly OpenApiStore _store;
    private readonly InMemoryVectorStore _vectors;
    private readonly OpenApiEvidenceBuilder _openApiEvidence;
    private readonly ILogger<RagTools> _logger;

    public RagTools(
        RagRuntime rag,
        OpenApiStore store,
        InMemoryVectorStore vectors,
        OpenApiEvidenceBuilder openApiEvidence,
        ILogger<RagTools> logger)
    {
        _rag = rag;
        _store = store;
        _vectors = vectors;
        _openApiEvidence = openApiEvidence;
        _logger = logger;
    }

    [McpServerTool, Description("Index the currently loaded OpenAPI contract into the in-memory vector store as operation, schema and security evidence.")]
    public async Task<object> ApiRagIndex(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var snapshot = _store.RequireSnapshot();
        var document = snapshot.Document;

        var spec = new OpenApiSpecRecord(
            snapshot.SourceId,
            snapshot.SessionId,
            Guid.Empty,
            document.Info?.Title ?? "(no title)",
            document.Info?.Version ?? "(no version)",
            snapshot.RawSpec,
            snapshot.SpecHash,
            snapshot.LoadedUtc);

        IReadOnlyList<ApiTester.Rag.Models.RagChunk> chunks = _openApiEvidence.Build(spec);
        var genericFallbackChunks = 0;
        if (chunks.Count == 0)
        {
            chunks = _rag.Chunker.Chunk(
                projectId: snapshot.SessionId,
                sourceType: "openapi",
                sourceId: snapshot.SourceId.ToString(),
                text: snapshot.RawSpec,
                metadata: new Dictionary<string, string>
                {
                    ["Title"] = spec.Title,
                    ["Version"] = spec.Version,
                    ["EvidenceType"] = "generic-fallback"
                },
                createdUtc: snapshot.LoadedUtc);
            genericFallbackChunks = chunks.Count;
        }

        _vectors.Clear(snapshot.SessionId);
        await _rag.Indexer.IndexAsync(chunks, ct).ConfigureAwait(false);

        var operationChunks = chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("operation", StringComparison.OrdinalIgnoreCase));
        var schemaChunks = chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("schema", StringComparison.OrdinalIgnoreCase));
        var securityChunks = chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("security", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("Indexed {ChunkCount} in-memory OpenAPI evidence chunks", chunks.Count);

        return new
        {
            ok = true,
            title = spec.Title,
            version = spec.Version,
            indexedChunks = chunks.Count,
            evidence = new
            {
                operations = operationChunks,
                schemas = schemaChunks,
                securitySchemes = securityChunks,
                genericFallback = genericFallbackChunks
            },
            persistence = "none"
        };
    }

    [McpServerTool, Description("Ask a grounded question about the currently indexed OpenAPI evidence.")]
    public async Task<object> ApiRagAsk(string question, int topK = 10, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(question))
            return new { ok = false, reason = "Question is required." };

        var snapshot = _store.RequireSnapshot();
        var result = await _rag.Answerer.AnswerAsync(snapshot.SessionId, question.Trim(), topK, ct).ConfigureAwait(false);

        return new
        {
            ok = true,
            answer = result.Answer,
            evidence = result.Evidence.Select(e => new
            {
                chunkId = e.Chunk.ChunkId,
                sourceType = e.Chunk.SourceType,
                sourceId = e.Chunk.SourceId,
                evidenceType = e.Chunk.Metadata.TryGetValue("EvidenceType", out var evidenceType) ? evidenceType : "unknown",
                operationId = e.Chunk.Metadata.TryGetValue("OperationId", out var operationId) ? operationId : null,
                schemaName = e.Chunk.Metadata.TryGetValue("SchemaName", out var schemaName) ? schemaName : null,
                score = e.Score,
                preview = e.Chunk.Text.Length <= 300 ? e.Chunk.Text : e.Chunk.Text[..300] + "…"
            }).ToList()
        };
    }
}
