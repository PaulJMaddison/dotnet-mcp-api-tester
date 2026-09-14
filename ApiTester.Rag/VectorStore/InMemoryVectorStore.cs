using ApiTester.Rag.Models;

namespace ApiTester.Rag.VectorStore;

public sealed class InMemoryVectorStore : IVectorStore
{
    private sealed record Stored(RagChunk Chunk, float[] Embedding);

    private readonly List<Stored> _items = new();
    private readonly object _gate = new();

    public Task UpsertAsync(IReadOnlyList<(RagChunk Chunk, float[] Embedding)> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            foreach (var (chunk, embedding) in items)
            {
                ct.ThrowIfCancellationRequested();
                if (chunk is null) throw new ArgumentException("Chunk is required.", nameof(items));
                if (embedding is null || embedding.Length == 0) throw new ArgumentException("Embedding is required.", nameof(items));

                var ownedEmbedding = embedding.ToArray();
                var index = _items.FindIndex(x =>
                    x.Chunk.ScopeId == chunk.ScopeId &&
                    x.Chunk.ChunkId == chunk.ChunkId);

                if (index >= 0)
                    _items[index] = new Stored(chunk, ownedEmbedding);
                else
                    _items.Add(new Stored(chunk, ownedEmbedding));
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RagRetrievedChunk>> QueryAsync(
        Guid scopeId,
        float[] embedding,
        int topK,
        IReadOnlyDictionary<string, string>? filters,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (scopeId == Guid.Empty) throw new ArgumentException("scopeId is required.", nameof(scopeId));
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length == 0) throw new ArgumentException("embedding must not be empty.", nameof(embedding));
        if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));

        List<Stored> snapshot;
        lock (_gate)
            snapshot = _items.Where(x => x.Chunk.ScopeId == scopeId).ToList();

        if (filters is { Count: > 0 })
            snapshot = snapshot.Where(x => MatchesFilters(x.Chunk, filters)).ToList();

        var results = snapshot
            .Select(x => new RagRetrievedChunk(x.Chunk, CosineSimilarity(embedding, x.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<RagRetrievedChunk>>(results);
    }

    public void Clear(Guid scopeId)
    {
        if (scopeId == Guid.Empty) return;
        lock (_gate)
            _items.RemoveAll(x => x.Chunk.ScopeId == scopeId);
    }

    private static bool MatchesFilters(RagChunk chunk, IReadOnlyDictionary<string, string> filters)
    {
        foreach (var (key, expected) in filters)
        {
            if (key.Equals("SourceType", StringComparison.OrdinalIgnoreCase))
            {
                if (!chunk.SourceType.Equals(expected, StringComparison.OrdinalIgnoreCase)) return false;
                continue;
            }

            if (key.Equals("SourceId", StringComparison.OrdinalIgnoreCase))
            {
                if (!chunk.SourceId.Equals(expected, StringComparison.OrdinalIgnoreCase)) return false;
                continue;
            }

            if (!chunk.Metadata.Any(pair =>
                    pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                    pair.Value.Equals(expected, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) throw new InvalidOperationException("Embedding dimension mismatch.");

        var dot = 0f;
        var na = 0f;
        var nb = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na <= 0f || nb <= 0f) return 0f;
        return dot / ((float)Math.Sqrt(na) * (float)Math.Sqrt(nb));
    }
}
