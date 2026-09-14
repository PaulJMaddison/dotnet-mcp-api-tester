using System.Net;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Models;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiTester.McpServer.Tests;

public sealed class OpenApiLoadAtomicityTests
{
    [Fact]
    public async Task LoadPublishesContractOnlyAfterSemanticIndexCompletes()
    {
        var store = new OpenApiStore();
        var vectors = new InMemoryVectorStore();
        var embeddings = new ConstantBatchEmbeddingClient();
        var tools = CreateTools(store, vectors, embeddings);
        var file = await TempSpecAsync("""
        {
          "openapi":"3.0.1","info":{"title":"Orders","version":"1"},
          "paths":{"/orders":{"get":{"operationId":"getOrders","responses":{"200":{"description":"OK"}}}}},
          "components":{"schemas":{"Order":{"type":"object","properties":{"id":{"type":"integer"}}}},"securitySchemes":{"bearer":{"type":"http","scheme":"bearer"}}}
        }
        """);

        try
        {
            await tools.ApiLoadOpenApi(file);

            var current = store.RequireSnapshot();
            Assert.Equal("Orders", current.Document.Info.Title);
            Assert.Equal(1, embeddings.BatchCalls);

            var indexed = await vectors.QueryAsync(current.ScopeId, new[] { 1f, 0f }, 20, null, CancellationToken.None);
            Assert.Equal(3, indexed.Count);
            Assert.Contains(indexed, x => x.Chunk.Metadata["EvidenceType"] == "operation");
            Assert.Contains(indexed, x => x.Chunk.Metadata["EvidenceType"] == "schema");
            Assert.Contains(indexed, x => x.Chunk.Metadata["EvidenceType"] == "security");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task FailedEmbeddingLeavesPreviousContractAndIndexUsable()
    {
        var store = new OpenApiStore();
        var vectors = new InMemoryVectorStore();
        var oldScope = Guid.NewGuid();
        var oldSource = Guid.NewGuid();
        var oldDocument = TestDocument("Old");
        store.SetDocument(oldScope, oldSource, oldDocument, "old.json", "old-hash", DateTime.UtcNow);
        await vectors.UpsertAsync(new[]
        {
            (new RagChunk(oldScope, "openapi", oldSource.ToString(), "old", "old evidence", "old-hash", DateTime.UtcNow, new Dictionary<string, string> { ["EvidenceType"] = "operation" }), new[] { 1f, 0f })
        }, CancellationToken.None);

        var tools = CreateTools(store, vectors, new FailingEmbeddingClient());
        var file = await TempSpecAsync("""
        {"openapi":"3.0.1","info":{"title":"New","version":"1"},"paths":{"/new":{"get":{"operationId":"getNew","responses":{"200":{"description":"OK"}}}}}}
        """);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => tools.ApiLoadOpenApi(file));

            Assert.Equal(oldScope, store.RequireSnapshot().ScopeId);
            Assert.Equal("Old", store.RequireDocument().Info.Title);
            var oldIndex = await vectors.QueryAsync(oldScope, new[] { 1f, 0f }, 10, null, CancellationToken.None);
            Assert.Single(oldIndex);
            Assert.Equal("old", oldIndex[0].Chunk.ChunkId);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task ContractWithNoSemanticEvidenceFailsWithoutReplacingPreviousState()
    {
        var store = new OpenApiStore();
        var vectors = new InMemoryVectorStore();
        var oldScope = Guid.NewGuid();
        var oldSource = Guid.NewGuid();
        store.SetDocument(oldScope, oldSource, TestDocument("Old"), "old.json", "old-hash", DateTime.UtcNow);
        var tools = CreateTools(store, vectors, new ConstantBatchEmbeddingClient());
        var file = await TempSpecAsync("""
        {"openapi":"3.0.1","info":{"title":"Empty","version":"1"},"paths":{}}
        """);

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tools.ApiLoadOpenApi(file));
            Assert.Contains("no operation, schema or security evidence", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(oldScope, store.RequireSnapshot().ScopeId);
            Assert.Equal("Old", store.RequireDocument().Info.Title);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task MissingFileFailsWithoutCreatingState()
    {
        var store = new OpenApiStore();
        var tools = CreateTools(store, new InMemoryVectorStore(), new ConstantBatchEmbeddingClient());

        await Assert.ThrowsAsync<FileNotFoundException>(() => tools.ApiLoadOpenApi(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
        Assert.False(store.HasDocument);
    }

    private static OpenApiTools CreateTools(OpenApiStore store, InMemoryVectorStore vectors, IEmbeddingClient embeddings)
    {
        var runtime = new ApiRuntimeConfig();
        var rag = new RagRuntime(new NeverCalledChat(), embeddings, vectors);
        return new OpenApiTools(
            store,
            runtime,
            new StubHttpClientFactory(),
            new SsrfGuard(),
            new OpenApiEvidenceBuilder(),
            rag,
            vectors,
            NullLogger<OpenApiTools>.Instance);
    }

    private static Microsoft.OpenApi.Models.OpenApiDocument TestDocument(string title)
    {
        var reader = new Microsoft.OpenApi.Readers.OpenApiStringReader();
        var doc = reader.Read($$"""
        {"openapi":"3.0.1","info":{"title":"{{title}}","version":"1"},"paths":{"/old":{"get":{"operationId":"getOld","responses":{"200":{"description":"OK"}}}}}}
        """, out var diagnostics);
        Assert.NotNull(doc);
        Assert.Empty(diagnostics.Errors);
        return doc;
    }

    private static async Task<string> TempSpecAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"apitester-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private sealed class ConstantBatchEmbeddingClient : IBatchEmbeddingClient
    {
        public int BatchCalls { get; private set; }
        public Task<float[]> EmbedAsync(string text, CancellationToken ct) => Task.FromResult(new[] { 1f, 0f });
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new[] { 1f, 0f }).ToList());
        }
    }

    private sealed class FailingEmbeddingClient : IBatchEmbeddingClient
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct) => throw new InvalidOperationException("embedding failed");
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct) => throw new InvalidOperationException("embedding failed");
    }

    private sealed class NeverCalledChat : IChatCompletionClient
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
            => throw new InvalidOperationException("chat should not be called while indexing");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler());
        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
