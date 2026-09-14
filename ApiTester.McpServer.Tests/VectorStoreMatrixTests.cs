using ApiTester.Rag.Models;
using ApiTester.Rag.VectorStore;

namespace ApiTester.McpServer.Tests;

public sealed class VectorStoreMatrixTests
{
    [Fact]
    public async Task UpsertReplacesSameChunkWithinScopeButNotOtherScopes()
    {
        var scope = Guid.NewGuid();
        var other = Guid.NewGuid();
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(new[]
        {
            (Chunk(scope, "same", "old"), new[] { 1f, 0f }),
            (Chunk(other, "same", "other"), new[] { 1f, 0f })
        }, CancellationToken.None);

        await store.UpsertAsync(new[] { (Chunk(scope, "same", "new"), new[] { 0f, 1f }) }, CancellationToken.None);

        var scoped = await store.QueryAsync(scope, new[] { 0f, 1f }, 10, null, CancellationToken.None);
        var otherScoped = await store.QueryAsync(other, new[] { 1f, 0f }, 10, null, CancellationToken.None);

        Assert.Single(scoped);
        Assert.Equal("new", scoped[0].Chunk.Text);
        Assert.Single(otherScoped);
        Assert.Equal("other", otherScoped[0].Chunk.Text);
    }

    [Fact]
    public async Task QueryHonoursTopKAndAllSupportedFiltersCaseInsensitively()
    {
        var scope = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[]
        {
            (Chunk(scope, "one", "orders", sourceType: "openapi", sourceId: "spec-A", metadata: new() { ["EvidenceType"] = "operation", ["Method"] = "GET" }), new[] { 1f, 0f }),
            (Chunk(scope, "two", "schema", sourceType: "openapi", sourceId: "spec-A", metadata: new() { ["EvidenceType"] = "schema" }), new[] { 0.9f, 0.1f }),
            (Chunk(scope, "three", "other source", sourceType: "notes", sourceId: "spec-B", metadata: new() { ["EvidenceType"] = "operation", ["Method"] = "GET" }), new[] { 0.8f, 0.2f })
        }, CancellationToken.None);

        var filtered = await store.QueryAsync(scope, new[] { 1f, 0f }, 1,
            new Dictionary<string, string>
            {
                ["sourcetype"] = "OPENAPI",
                ["sourceid"] = "SPEC-A",
                ["evidencetype"] = "OPERATION",
                ["method"] = "get"
            }, CancellationToken.None);

        var item = Assert.Single(filtered);
        Assert.Equal("one", item.Chunk.ChunkId);
    }

    [Fact]
    public async Task ClearRemovesOnlyRequestedScope()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[]
        {
            (Chunk(a, "a", "A"), new[] { 1f, 0f }),
            (Chunk(b, "b", "B"), new[] { 1f, 0f })
        }, CancellationToken.None);

        store.Clear(a);

        Assert.Empty(await store.QueryAsync(a, new[] { 1f, 0f }, 10, null, CancellationToken.None));
        Assert.Single(await store.QueryAsync(b, new[] { 1f, 0f }, 10, null, CancellationToken.None));
    }

    [Fact]
    public async Task ZeroVectorsScoreZeroRatherThanNaN()
    {
        var scope = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[] { (Chunk(scope, "zero", "zero"), new[] { 0f, 0f }) }, CancellationToken.None);

        var result = await store.QueryAsync(scope, new[] { 1f, 0f }, 1, null, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(0f, result[0].Score);
        Assert.False(float.IsNaN(result[0].Score));
    }

    [Fact]
    public async Task DimensionMismatchFailsClosed()
    {
        var scope = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[] { (Chunk(scope, "x", "x"), new[] { 1f, 0f, 0f }) }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.QueryAsync(scope, new[] { 1f, 0f }, 1, null, CancellationToken.None));
    }

    [Fact]
    public async Task InvalidInputsAreRejected()
    {
        var store = new InMemoryVectorStore();
        var scope = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() => store.UpsertAsync(new[] { (Chunk(scope, "x", "x"), Array.Empty<float>()) }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.QueryAsync(Guid.Empty, new[] { 1f }, 1, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => store.QueryAsync(scope, Array.Empty<float>(), 1, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.QueryAsync(scope, new[] { 1f }, 0, null, CancellationToken.None));
    }

    private static RagChunk Chunk(
        Guid scope,
        string id,
        string text,
        string sourceType = "openapi",
        string sourceId = "spec",
        Dictionary<string, string>? metadata = null)
        => new(scope, sourceType, sourceId, id, text, id + "-hash", DateTime.UtcNow, metadata ?? new Dictionary<string, string>());
}
