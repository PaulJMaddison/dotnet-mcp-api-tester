using ApiTester.McpServer.Rag;
using ApiTester.Rag.Models;
using ApiTester.Rag.Prompting;
using ApiTester.Rag.VectorStore;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class RagCoreTests
{
    [Fact]
    public void OpenApiEvidenceBuilder_PreservesSemanticBoundaries()
    {
        var document = new OpenApiStringReader().Read("""
        {
          "openapi":"3.0.1",
          "info":{"title":"Orders","version":"1"},
          "paths":{"/orders":{"get":{"operationId":"getOrders","parameters":[{"name":"from","in":"query","schema":{"type":"string","format":"date"}}],"responses":{"200":{"description":"OK","content":{"application/json":{"schema":{"$ref":"#/components/schemas/Order"}}}}},"security":[{"bearerAuth":[]}]}}},
          "components":{"schemas":{"Order":{"type":"object","required":["id"],"properties":{"id":{"type":"integer","format":"int64"}}}},"securitySchemes":{"bearerAuth":{"type":"http","scheme":"bearer"}}}
        }
        """, out var diagnostics);

        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);

        var chunks = new OpenApiEvidenceBuilder().Build(
            document!, Guid.NewGuid(), Guid.NewGuid(), "Orders", "1", DateTime.UtcNow);

        Assert.Contains(chunks, c => c.Metadata["EvidenceType"] == "operation" && c.Text.Contains("GET") && c.Text.Contains("/orders"));
        Assert.Contains(chunks, c => c.Metadata["EvidenceType"] == "schema" && c.Text.Contains("SCHEMA: Order"));
        Assert.Contains(chunks, c => c.Metadata["EvidenceType"] == "security" && c.Text.Contains("bearerAuth"));
    }

    [Fact]
    public async Task InMemoryVectorStore_RanksByCosineSimilarityWithinScope()
    {
        var scope = Guid.NewGuid();
        var otherScope = Guid.NewGuid();
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(new[]
        {
            (Chunk(scope, "orders"), new[] { 1f, 0f }),
            (Chunk(scope, "customers"), new[] { 0f, 1f }),
            (Chunk(otherScope, "other"), new[] { 1f, 0f })
        }, CancellationToken.None);

        var result = await store.QueryAsync(scope, new[] { 0.9f, 0.1f }, 2, null, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("orders", result[0].Chunk.ChunkId);
        Assert.DoesNotContain(result, item => item.Chunk.ScopeId == otherScope);
    }

    [Fact]
    public void GroundingPrompt_ExplicitlyRejectsUnsupportedSpeculation()
    {
        var prompt = new RagPromptBuilder().SystemPrompt;
        Assert.Contains("only the evidence", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do NOT mention plausible", prompt, StringComparison.Ordinal);
        Assert.Contains("untrusted DATA", prompt, StringComparison.OrdinalIgnoreCase);
    }

    private static RagChunk Chunk(Guid scopeId, string id) => new(
        scopeId,
        "openapi",
        "source",
        id,
        id,
        id,
        DateTime.UtcNow,
        new Dictionary<string, string> { ["EvidenceType"] = "operation" });
}
