using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Models;
using ApiTester.Rag.Prompting;
using ApiTester.Rag.VectorStore;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class RagAnswerMatrixTests
{
    [Fact]
    public async Task NoEvidenceReturnsExplicitGroundedFallbackWithoutCallingChat()
    {
        var embeddings = new RecordingEmbeddingClient(new[] { 1f, 0f });
        var store = new InMemoryVectorStore();
        var chat = new RecordingChatClient();
        var service = new RagAnswerService(embeddings, store, new RagPromptBuilder(), chat);

        var answer = await service.AnswerAsync(Guid.NewGuid(), "where are orders?", 10, CancellationToken.None);

        Assert.Contains("do not have indexed OpenAPI evidence", answer.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(answer.Evidence);
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task TopKIsClampedToTwenty()
    {
        var scope = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(Enumerable.Range(0, 25)
            .Select(i => (Chunk(scope, i), new[] { 1f, i / 100f }))
            .ToList(), CancellationToken.None);
        var chat = new RecordingChatClient();
        var service = new RagAnswerService(new RecordingEmbeddingClient(new[] { 1f, 0f }), store, new RagPromptBuilder(), chat);

        var answer = await service.AnswerAsync(scope, "orders", 1000, CancellationToken.None);

        Assert.Equal(20, answer.Evidence.Count);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task TopKIsClampedToAtLeastOne()
    {
        var scope = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[] { (Chunk(scope, 1), new[] { 1f, 0f }), (Chunk(scope, 2), new[] { 0.9f, 0.1f }) }, CancellationToken.None);
        var service = new RagAnswerService(new RecordingEmbeddingClient(new[] { 1f, 0f }), store, new RagPromptBuilder(), new RecordingChatClient());

        var answer = await service.AnswerAsync(scope, "orders", 0, CancellationToken.None);

        Assert.Single(answer.Evidence);
    }

    [Fact]
    public async Task QuestionIsTrimmedBeforeEmbeddingAndPromptIsGroundedWithRetrievedChunkIds()
    {
        var scope = Guid.NewGuid();
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new[] { (Chunk(scope, 1), new[] { 1f, 0f }) }, CancellationToken.None);
        var embeddings = new RecordingEmbeddingClient(new[] { 1f, 0f });
        var chat = new RecordingChatClient { Response = "grounded answer" };
        var service = new RagAnswerService(embeddings, store, new RagPromptBuilder(), chat);

        var answer = await service.AnswerAsync(scope, "   find orders   ", 5, CancellationToken.None);

        Assert.Equal("find orders", embeddings.LastText);
        Assert.Equal("grounded answer", answer.Answer);
        Assert.Contains("USER QUESTION\nfind orders", chat.LastUserPrompt);
        Assert.Contains("[chunk:chunk-1]", chat.LastUserPrompt);
        Assert.Contains("BEGIN UNTRUSTED API EVIDENCE", chat.LastUserPrompt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankQuestionsAreRejected(string question)
    {
        var service = new RagAnswerService(new RecordingEmbeddingClient(new[] { 1f }), new InMemoryVectorStore(), new RagPromptBuilder(), new RecordingChatClient());
        await Assert.ThrowsAsync<ArgumentException>(() => service.AnswerAsync(Guid.NewGuid(), question, 5, CancellationToken.None));
    }

    [Fact]
    public async Task SemanticSearchUsesOnlyCurrentLoadedScope()
    {
        var currentScope = Guid.NewGuid();
        var oldScope = Guid.NewGuid();
        var store = new OpenApiStore();
        var document = new OpenApiStringReader().Read("""
        {"openapi":"3.0.1","info":{"title":"Current","version":"1"},"paths":{"/orders":{"get":{"operationId":"getOrders","responses":{"200":{"description":"OK"}}}}}}
        """, out var diagnostics);
        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);
        store.SetDocument(currentScope, Guid.NewGuid(), document, "current", "hash", DateTime.UtcNow);

        var vectors = new InMemoryVectorStore();
        await vectors.UpsertAsync(new[]
        {
            (new RagChunk(currentScope, "openapi", "current", "current-op", "current orders", "h1", DateTime.UtcNow, new Dictionary<string,string>{{"EvidenceType","operation"},{"OperationId","getOrders"}}), new[] { 1f, 0f }),
            (new RagChunk(oldScope, "openapi", "old", "old-op", "old orders", "h2", DateTime.UtcNow, new Dictionary<string,string>{{"EvidenceType","operation"},{"OperationId","oldOrders"}}), new[] { 1f, 0f })
        }, CancellationToken.None);

        var embeddings = new RecordingEmbeddingClient(new[] { 1f, 0f });
        var rag = new RagRuntime(new RecordingChatClient(), embeddings, vectors);
        var tools = new RagTools(store, embeddings, vectors, rag);
        var json = System.Text.Json.JsonSerializer.Serialize(await tools.ApiSearchContract("orders", 10));

        Assert.Contains("getOrders", json);
        Assert.DoesNotContain("oldOrders", json);
        Assert.DoesNotContain("old orders", json);
    }

    private static RagChunk Chunk(Guid scope, int i) => new(
        scope,
        "openapi",
        "spec",
        $"chunk-{i}",
        $"operation evidence {i}",
        $"hash-{i}",
        DateTime.UtcNow,
        new Dictionary<string, string> { ["EvidenceType"] = "operation", ["OperationId"] = $"op{i}" });

    private sealed class RecordingEmbeddingClient : IEmbeddingClient
    {
        private readonly float[] _vector;
        public string? LastText { get; private set; }
        public RecordingEmbeddingClient(float[] vector) => _vector = vector;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct)
        {
            LastText = text;
            return Task.FromResult(_vector.ToArray());
        }
    }

    private sealed class RecordingChatClient : IChatCompletionClient
    {
        public int CallCount { get; private set; }
        public string? LastSystemPrompt { get; private set; }
        public string? LastUserPrompt { get; private set; }
        public string Response { get; set; } = "answer";
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
        {
            CallCount++;
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            return Task.FromResult(Response);
        }
    }
}
