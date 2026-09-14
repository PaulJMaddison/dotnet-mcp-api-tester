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

        foreach (var chunk in chunks)
        {
            if (chunk is null)
                throw new ArgumentException("Chunk collection must not contain null entries.", nameof(chunks));
        }

        IReadOnlyList<float[]> embeddings;
        if (_embeddings is IBatchEmbeddingClient batchClient)
        {
            embeddings = await batchClient
                .EmbedBatchAsync(chunks.Select(c => c.Text).ToList(), ct)
                .ConfigureAwait(false);

            if (embeddings.Count != chunks.Count)
                throw new InvalidOperationException(
                    $"Embedding provider returned {embeddings.Count} vectors for {chunks.Count} chunks.");
        }
        else
        {
            var vectors = new List<float[]>(chunks.Count);
            foreach (var chunk in chunks)
            {
                ct.ThrowIfCancellationRequested();
                vectors.Add(await _embeddings.EmbedAsync(chunk.Text, ct).ConfigureAwait(false));
            }
            embeddings = vectors;
        }

        // Publish only after every embedding has completed successfully. A failed
        // batch cannot leave a partially indexed project in the vector store.
        var items = chunks.Select((chunk, index) => (chunk, embeddings[index])).ToList();
        await _store.UpsertAsync(items, ct).ConfigureAwait(false);
    }
}
