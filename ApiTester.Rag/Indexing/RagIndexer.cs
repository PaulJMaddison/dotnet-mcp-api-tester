using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Models;
using ApiTester.Rag.VectorStore;

namespace ApiTester.Rag.Indexing;

public sealed class RagIndexer
{
    private readonly IEmbeddingClient _embeddings;
    private readonly IVectorStore _store;

    public RagIndexer(IEmbeddingClient embeddings, IVectorStore store)
    {
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task IndexAsync(IReadOnlyList<RagChunk> chunks, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ct.ThrowIfCancellationRequested();
        if (chunks.Count == 0) return;

        var items = new List<(RagChunk Chunk, float[] Embedding)>(chunks.Count);
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            if (chunk is null)
                throw new ArgumentException("Chunk collection must not contain null entries.", nameof(chunks));

            var embedding = await _embeddings.EmbedAsync(chunk.Text, ct).ConfigureAwait(false);
            items.Add((chunk, embedding));
        }

        await _store.UpsertAsync(items, ct).ConfigureAwait(false);
    }
}
