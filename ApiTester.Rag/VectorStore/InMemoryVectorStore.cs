using ApiTester.Rag.Models;

namespace ApiTester.Rag.VectorStore;

public sealed class InMemoryVectorStore : IVectorStore
{
    private sealed record Stored(RagChunk Chunk, float[] Embedding);

    private readonly List<Stored> _items = new();
    private readonly object _lock = new();

    public Task UpsertAsync(IReadOnlyList<(RagChunk Chunk, float[] Embedding)> items, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (items.Count == 0) return Task.CompletedTask;

        lock (_lock)
        {
            foreach (var (chunk, embedding) in items)
            {
                var idx = _items.FindIndex(x =>
                    x.Chunk.ProjectId == chunk.ProjectId &&
                    x.Chunk.ChunkId == chunk.ChunkId);

                if (idx >= 0)
                {
                    if (_items[idx].Chunk.ContentHash == chunk.ContentHash)
                        continue;

                    _items[idx] = new Stored(chunk, embedding);
                }
                else
                {
                    _items.Add(new Stored(chunk, embedding));
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RagRetrievedChunk>> QueryAsync(
        Guid projectId,
        float[] embedding,
        int topK,
        IReadOnlyDictionary<string, string>? filters,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));

        List<Stored> snapshot;
        lock (_lock)
        {
            snapshot = _items.Where(x => x.Chunk.ProjectId == projectId).ToList();
        }

        if (filters is { Count: > 0 })
            snapshot = snapshot.Where(x => MatchesFilters(x.Chunk, filters)).ToList();

        var results = snapshot
            .Select(x => new RagRetrievedChunk(x.Chunk, CosineSimilarity(embedding, x.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<RagRetrievedChunk>>(results);
    }

    private static bool MatchesFilters(RagChunk chunk, IReadOnlyDictionary<string, string> filters)
    {
        foreach (var (k, v) in filters)
        {
            if (k.Equals("SourceType", StringComparison.OrdinalIgnoreCase))
            {
                if (!chunk.SourceType.Equals(v, StringComparison.OrdinalIgnoreCase)) return false;
                continue;
            }

            if (k.Equals("SourceId", StringComparison.OrdinalIgnoreCase))
            {
                if (!chunk.SourceId.Equals(v, StringComparison.OrdinalIgnoreCase)) return false;
                continue;
            }

            if (!chunk.Metadata.TryGetValue(k, out var actual)) return false;
            if (!actual.Equals(v, StringComparison.OrdinalIgnoreCase)) return false;
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
