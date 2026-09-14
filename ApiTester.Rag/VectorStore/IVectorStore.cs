using ApiTester.Rag.Models;

namespace ApiTester.Rag.VectorStore;

public interface IVectorStore
{
    Task UpsertAsync(IReadOnlyList<(RagChunk Chunk, float[] Embedding)> items, CancellationToken ct);

    Task<IReadOnlyList<RagRetrievedChunk>> QueryAsync(
        Guid scopeId,
        float[] embedding,
        int topK,
        IReadOnlyDictionary<string, string>? filters,
        CancellationToken ct);
}
