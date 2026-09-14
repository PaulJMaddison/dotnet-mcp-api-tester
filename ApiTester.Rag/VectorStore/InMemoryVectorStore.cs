using ApiTester.Rag.Models;

namespace ApiTester.Rag.VectorStore;

public sealed class InMemoryVectorStore : IVectorStore
{
    private sealed record Stored(RagChunk Chunk, float[] Embedding);

    private readonly List<Stored> _items = new();
    private readonly object _lock = new();

    public Task UpsertAsync(IReadOnlyList<(RagChunk Chunk, float[] Embedding)> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ct.ThrowIfCancellationRequested();
        if (items.Count == 0) return Task.CompletedTask;

        lock (_lock)
        {
            foreach (var (chunk, embedding) in items)
            {
                ct.ThrowIfCancellationRequested();
                if (chunk is null)
                    throw new ArgumentException("Vector-store items must contain a chunk.", nameof(items));
                if (embedding is null || embedding.Length == 0)
                    throw new ArgumentException("Vector-store items must contain a non-empty embedding.", nameof(items));

                var ownedEmbedding = embedding.ToArray();
                var index = _items.FindIndex(x =>
                    x.Chunk.ProjectId == chunk.ProjectId &&
                    x.Chunk.ChunkId == chunk.ChunkId);

                if (index >= 0)
                {
                    var existing = _items[index];
                    if (existing.Chunk.ContentHash == chunk.ContentHash &&
                        existing.Embedding.AsSpan().SequenceEqual(ownedEmbedding))
                        continue;

                    _items[index] = new Stored(chunk, ownedEmbedding);
                }
                else
                {
                    _items.Add(new Stored(chunk, ownedEmbedding));
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
        if (projectId == Guid.Empty) throw new ArgumentException("projectId required", nameof(projectId));
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length == 0) throw new ArgumentException("embedding must not be empty", nameof(embedding));
        if (topK <= 0) throw new ArgumentOutOfRangeException(nameof(topK));

        List<Stored> snapshot;
        lock (_lock)
            snapshot = _items.Where(x => x.Chunk.ProjectId == projectId).ToList();

        if (filters is { Count: > 0 })
            snapshot = snapshot.Where(x => MatchesFilters(x.Chunk, filters)).ToList();

        var results = snapshot
            .Select(x => new RagRetrievedChunk(x.Chunk, CosineSimilarity(embedding, x.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<RagRetrievedChunk>>(results);
    }

    public void Clear(Guid projectId)
    {
        if (projectId == Guid.Empty) return;
        lock (_lock)
            _items.RemoveAll(x => x.Chunk.ProjectId == projectId);
    }

    public void ClearAll()
    {
        lock (_lock)
            _items.Clear();
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

            var found = false;
            foreach (var (metadataKey, actual) in chunk.Metadata)
            {
                if (!metadataKey.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                found = true;
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) return false;
                break;
            }

            if (!found) return false;
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
