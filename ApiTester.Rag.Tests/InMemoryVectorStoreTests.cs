using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Models;
using ApiTester.Rag.VectorStore;
using Xunit;

namespace ApiTester.Rag.Tests;

public sealed class InMemoryVectorStoreTests
{
    [Fact]
    public async Task Query_IsolatedByProjectId()
    {
        var embeddings = new DeterministicHashEmbeddingClient(64);
        var store = new InMemoryVectorStore();

        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var c1 = new RagChunk(projectA, "openapi", "s1", "c1", "alpha", "h1", DateTime.UtcNow, new Dictionary<string, string>());
        var c2 = new RagChunk(projectB, "openapi", "s1", "c2", "alpha", "h2", DateTime.UtcNow, new Dictionary<string, string>());

        await store.UpsertAsync(new[]
        {
            (c1, await embeddings.EmbedAsync(c1.Text, default)),
            (c2, await embeddings.EmbedAsync(c2.Text, default))
        }, default);

        var q = await embeddings.EmbedAsync("alpha", default);
        var results = await store.QueryAsync(projectA, q, topK: 10, filters: null, ct: default);

        Assert.Single(results);
        Assert.Equal("c1", results[0].Chunk.ChunkId);
    }
}
