using System.ComponentModel;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.VectorStore;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class RagTools
{
    private readonly OpenApiStore _store;
    private readonly IEmbeddingClient _embeddings;
    private readonly InMemoryVectorStore _vectors;
    private readonly RagRuntime _rag;
    private readonly QualificationTelemetry? _telemetry;

    public RagTools(OpenApiStore store, IEmbeddingClient embeddings, InMemoryVectorStore vectors, RagRuntime rag, QualificationTelemetry? telemetry = null)
    {
        _store = store;
        _embeddings = embeddings;
        _vectors = vectors;
        _rag = rag;
        _telemetry = telemetry;
    }

    [McpServerTool, Description("Search the loaded OpenAPI contract semantically and return the most relevant operation/schema/security evidence without calling the chat model.")]
    public async Task<object> ApiSearchContract(string query, int topK = 8, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("query is required.", nameof(query));

        var snapshot = _store.RequireSnapshot();
        _telemetry?.Emit("rag.query.start", new { toolName = "api_search_contract", queryLength = query.Trim().Length, topK });
        var vector = await _embeddings.EmbedAsync(query.Trim(), ct).ConfigureAwait(false);
        var evidence = await _vectors.QueryAsync(snapshot.ScopeId, vector, Math.Clamp(topK, 1, 20), null, ct).ConfigureAwait(false);
        _telemetry?.Emit("rag.retrieval.completed", new { scopeId = snapshot.ScopeId, vectorCount = evidence.Count, retrievedChunkIds = evidence.Select(x => x.Chunk.ChunkId).ToArray(), retrievalScores = evidence.Select(x => x.Score).ToArray() });

        return new
        {
            query = query.Trim(),
            evidence = evidence.Select(ToResult).ToList()
        };
    }

    [McpServerTool, Description("Ask Azure AI a grounded question about the loaded API using only retrieved OpenAPI evidence.")]
    public async Task<object> ApiAskContract(string question, int topK = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("question is required.", nameof(question));

        var snapshot = _store.RequireSnapshot();
        _telemetry?.Emit("rag.query.start", new { toolName = "api_ask_contract", queryLength = question.Trim().Length, topK });
        var result = await _rag.Answerer.AnswerAsync(snapshot.ScopeId, question.Trim(), topK, ct).ConfigureAwait(false);
        _telemetry?.Emit("rag.reasoning.completed", new { scopeId = snapshot.ScopeId, retrievedChunkIds = result.Evidence.Select(x => x.Chunk.ChunkId).ToArray(), retrievalScores = result.Evidence.Select(x => x.Score).ToArray(), success = true });
        _telemetry?.Emit("mcp.tool.end", new { toolName = "api_ask_contract", success = true });
        return new
        {
            answer = result.Answer,
            evidence = result.Evidence.Select(ToResult).ToList()
        };
    }

    private static object ToResult(ApiTester.Rag.Models.RagRetrievedChunk item) => new
    {
        chunkId = item.Chunk.ChunkId,
        evidenceType = item.Chunk.Metadata.TryGetValue("EvidenceType", out var evidenceType) ? evidenceType : "unknown",
        operationId = item.Chunk.Metadata.TryGetValue("OperationId", out var operationId) ? operationId : null,
        schemaName = item.Chunk.Metadata.TryGetValue("SchemaName", out var schemaName) ? schemaName : null,
        score = item.Score,
        text = item.Chunk.Text
    };
}
