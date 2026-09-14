using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Models;
using ApiTester.Rag.VectorStore;

namespace ApiTester.Rag.Tests;

public sealed class VectorStoreAndEmbeddingEdgeCaseTests
{
    [Fact]
    public void EmbeddingClient_DimensionsBelowMinimum_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicHashEmbeddingClient(31));
    }

    [Fact]
    public async Task EmbeddingClient_EmptyText_ReturnsZeroVectorWithConfiguredDimensions()
    {
        var client = new DeterministicHashEmbeddingClient(64);

        var vector = await client.EmbedAsync("   ", CancellationToken.None);

        Assert.Equal(64, vector.Length);
        Assert.All(vector, value => Assert.Equal(0f, value));
    }

    [Fact]
    public async Task EmbeddingClient_SameText_IsDeterministic()
    {
        var client = new DeterministicHashEmbeddingClient(128);

        var first = await client.EmbedAsync("GET /customers/{id} getCustomerById", CancellationToken.None);
        var second = await client.EmbedAsync("GET /customers/{id} getCustomerById", CancellationToken.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task EmbeddingClient_NormalisesNonEmptyVectorToUnitLength()
    {
        var client = new DeterministicHashEmbeddingClient(256);

        var vector = await client.EmbedAsync("POST /customers creates customer records", CancellationToken.None);
        var norm = Math.Sqrt(vector.Sum(v => v * v));

        Assert.InRange(norm, 0.9999, 1.0001);
    }

    [Fact]
    public async Task EmbeddingClient_SharedApiTermsProducePositiveSimilarity()
    {
        var client = new DeterministicHashEmbeddingClient(512);
        var a = await client.EmbedAsync("GET /customers/{id} returns customer by id", CancellationToken.None);
        var b = await client.EmbedAsync("How do I get customer id?", CancellationToken.None);

        var similarity = Dot(a, b);

        Assert.True(similarity > 0f);
    }

    [Fact]
    public async Task EmbeddingClient_CancelledToken_Throws()
    {
        var client = new DeterministicHashEmbeddingClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.EmbedAsync("customers", cts.Token));
    }

    [Fact]
    public async Task Store_EmptyUpsert_IsNoOp()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(Array.Empty<(RagChunk, float[])>(), CancellationToken.None);

        var results = await store.QueryAsync(Guid.NewGuid(), new float[] { 1, 0 }, 1, null, CancellationToken.None);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Store_SameChunkAndSameHash_DoesNotReplaceExistingEmbedding()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        var chunk = Chunk(projectId, "c1", "same-hash", "customer");

        await store.UpsertAsync(new[] { (chunk, new float[] { 1, 0 }) }, CancellationToken.None);
        await store.UpsertAsync(new[] { (chunk with { Text = "changed text but same content hash" }, new float[] { 0, 1 }) }, CancellationToken.None);

        var results = await store.QueryAsync(projectId, new float[] { 1, 0 }, 1, null, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(1f, results[0].Score, 5);
    }

    [Fact]
    public async Task Store_SameChunkAndDifferentHash_ReplacesEmbeddingAndContent()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        var original = Chunk(projectId, "c1", "hash-a", "customer old");
        var updated = Chunk(projectId, "c1", "hash-b", "customer new");

        await store.UpsertAsync(new[] { (original, new float[] { 1, 0 }) }, CancellationToken.None);
        await store.UpsertAsync(new[] { (updated, new float[] { 0, 1 }) }, CancellationToken.None);

        var results = await store.QueryAsync(projectId, new float[] { 0, 1 }, 1, null, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("customer new", result.Chunk.Text);
        Assert.Equal("hash-b", result.Chunk.ContentHash);
        Assert.Equal(1f, result.Score, 5);
    }

    [Fact]
    public async Task Store_ChunkIdentity_IsProjectScoped()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(new[]
        {
            (Chunk(projectA, "shared", "a", "alpha"), new float[] { 1, 0 }),
            (Chunk(projectB, "shared", "b", "beta"), new float[] { 0, 1 })
        }, CancellationToken.None);

        var a = await store.QueryAsync(projectA, new float[] { 1, 0 }, 10, null, CancellationToken.None);
        var b = await store.QueryAsync(projectB, new float[] { 0, 1 }, 10, null, CancellationToken.None);

        Assert.Single(a);
        Assert.Single(b);
        Assert.Equal("alpha", a[0].Chunk.Text);
        Assert.Equal("beta", b[0].Chunk.Text);
    }

    [Fact]
    public async Task Store_TopKLimitsReturnedResultsAndSortsDescending()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[]
        {
            (Chunk(projectId, "best", "1", "best"), new float[] { 1f, 0f }),
            (Chunk(projectId, "middle", "2", "middle"), new float[] { 0.8f, 0.6f }),
            (Chunk(projectId, "worst", "3", "worst"), new float[] { 0f, 1f })
        }, CancellationToken.None);

        var results = await store.QueryAsync(projectId, new float[] { 1f, 0f }, 2, null, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("best", results[0].Chunk.ChunkId);
        Assert.Equal("middle", results[1].Chunk.ChunkId);
        Assert.True(results[0].Score >= results[1].Score);
    }

    [Fact]
    public async Task Store_TopKZeroOrNegative_Throws()
    {
        var store = new InMemoryVectorStore();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.QueryAsync(Guid.NewGuid(), new float[] { 1 }, 0, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.QueryAsync(Guid.NewGuid(), new float[] { 1 }, -1, null, CancellationToken.None));
    }

    [Fact]
    public async Task Store_FiltersBySourceTypeAndSourceIdCaseInsensitively()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        var one = Chunk(projectId, "one", "1", "customers") with { SourceType = "OpenAPI", SourceId = "Spec-A" };
        var two = Chunk(projectId, "two", "2", "customers") with { SourceType = "docs", SourceId = "Spec-B" };

        await store.UpsertAsync(new[]
        {
            (one, new float[] { 1, 0 }),
            (two, new float[] { 1, 0 })
        }, CancellationToken.None);

        var results = await store.QueryAsync(
            projectId,
            new float[] { 1, 0 },
            10,
            new Dictionary<string, string>
            {
                ["sOuRcEtYpE"] = "openapi",
                ["SOURCEID"] = "spec-a"
            },
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("one", results[0].Chunk.ChunkId);
    }

    [Fact]
    public async Task Store_FiltersByMetadataCaseInsensitively()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        var matching = Chunk(projectId, "match", "1", "customer") with
        {
            Metadata = new Dictionary<string, string> { ["Version"] = "V2", ["Title"] = "Customer API" }
        };
        var other = Chunk(projectId, "other", "2", "customer") with
        {
            Metadata = new Dictionary<string, string> { ["Version"] = "v1" }
        };

        await store.UpsertAsync(new[]
        {
            (matching, new float[] { 1, 0 }),
            (other, new float[] { 1, 0 })
        }, CancellationToken.None);

        var results = await store.QueryAsync(
            projectId,
            new float[] { 1, 0 },
            10,
            new Dictionary<string, string> { ["Version"] = "v2" },
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("match", results[0].Chunk.ChunkId);
    }

    [Fact]
    public async Task Store_UnknownMetadataFilter_ReturnsNoResults()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[]
        {
            (Chunk(projectId, "one", "1", "customer"), new float[] { 1, 0 })
        }, CancellationToken.None);

        var results = await store.QueryAsync(
            projectId,
            new float[] { 1, 0 },
            10,
            new Dictionary<string, string> { ["DoesNotExist"] = "x" },
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Store_EmbeddingDimensionMismatch_Throws()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[]
        {
            (Chunk(projectId, "one", "1", "customer"), new float[] { 1, 0, 0 })
        }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.QueryAsync(projectId, new float[] { 1, 0 }, 1, null, CancellationToken.None));
    }

    [Fact]
    public async Task Store_ZeroVectorsHaveZeroSimilarity()
    {
        var projectId = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[]
        {
            (Chunk(projectId, "zero", "1", "empty"), new float[] { 0, 0 })
        }, CancellationToken.None);

        var results = await store.QueryAsync(projectId, new float[] { 0, 0 }, 1, null, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(0f, results[0].Score);
    }

    [Fact]
    public async Task Store_CancelledToken_StopsUpsertAndQuery()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var store = new InMemoryVectorStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.UpsertAsync(new[] { (Chunk(Guid.NewGuid(), "x", "x", "x"), new float[] { 1 }) }, cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.QueryAsync(Guid.NewGuid(), new float[] { 1 }, 1, null, cts.Token));
    }

    private static RagChunk Chunk(Guid projectId, string id, string hash, string text) =>
        new(
            projectId,
            "openapi",
            "spec",
            id,
            text,
            hash,
            DateTime.UtcNow,
            new Dictionary<string, string>());

    private static float Dot(float[] a, float[] b)
    {
        Assert.Equal(a.Length, b.Length);
        var result = 0f;
        for (var i = 0; i < a.Length; i++) result += a[i] * b[i];
        return result;
    }
}
