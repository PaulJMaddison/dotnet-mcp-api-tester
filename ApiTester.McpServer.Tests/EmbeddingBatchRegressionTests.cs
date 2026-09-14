using System.Net;
using System.Text;
using System.Text.Json;
using ApiTester.AI.Azure;
using ApiTester.McpServer.Rag;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Indexing;
using ApiTester.Rag.Models;
using ApiTester.Rag.VectorStore;

namespace ApiTester.McpServer.Tests;

public sealed class EmbeddingBatchRegressionTests
{
    [Fact]
    public async Task AzureEmbeddingClient_BatchesAndRestoresProviderOrder()
    {
        var handler = new EmbeddingHandler();
        var options = Options();
        var client = new AzureOpenAiEmbeddingClient(new AzureOpenAiTransport(new HttpClient(handler), options), options);

        var result = await client.EmbedBatchAsync(new[] { "one", "two", "three" }, CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(3, handler.LastInputCount);
        Assert.Equal(1f, result[0][0]);
        Assert.Equal(2f, result[1][0]);
        Assert.Equal(3f, result[2][0]);
    }

    [Fact]
    public async Task RagIndexer_UsesBatchCapabilityAndPublishesOnce()
    {
        var embeddings = new SuccessfulBatchEmbeddingClient();
        var store = new RecordingVectorStore();
        var indexer = new RagIndexer(embeddings, store);

        await indexer.IndexAsync(new[] { Chunk("a"), Chunk("b"), Chunk("c") }, CancellationToken.None);

        Assert.Equal(1, embeddings.BatchCalls);
        Assert.Equal(0, embeddings.SingleCalls);
        Assert.Equal(1, store.UpsertCalls);
        Assert.Equal(3, store.LastCount);
    }

    [Fact]
    public async Task RagIndexer_BatchFailure_DoesNotPublishPartialVectors()
    {
        var store = new RecordingVectorStore();
        var indexer = new RagIndexer(new FailingBatchEmbeddingClient(), store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            indexer.IndexAsync(new[] { Chunk("a"), Chunk("b") }, CancellationToken.None));

        Assert.Equal(0, store.UpsertCalls);
    }

    private static AzureOpenAiOptions Options() => new()
    {
        Endpoint = "https://example.openai.azure.com/openai/v1/",
        EmbeddingDeployment = "embedding-test",
        Authentication = AzureOpenAiOptions.ApiKeyAuthentication,
        ApiKey = "unit-test-key",
        MaxInputChars = 1000,
        MaxRetries = 0
    };

    private static RagChunk Chunk(string id) => new(
        ScopeId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        SourceType: "openapi",
        SourceId: "spec",
        ChunkId: id,
        Text: $"text-{id}",
        ContentHash: $"hash-{id}",
        CreatedUtc: DateTime.UtcNow,
        Metadata: new Dictionary<string, string>());

    private sealed class EmbeddingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public int LastInputCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            LastInputCount = document.RootElement.GetProperty("input").GetArrayLength();

            var data = Enumerable.Range(0, LastInputCount).Reverse()
                .Select(index => new { index, embedding = new[] { (float)(index + 1), 0.5f } }).ToArray();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { data }), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SuccessfulBatchEmbeddingClient : IBatchEmbeddingClient
    {
        public int BatchCalls { get; private set; }
        public int SingleCalls { get; private set; }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct)
        {
            SingleCalls++;
            return Task.FromResult(new[] { 1f, 0f });
        }

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyList<float[]>>(texts.Select((_, i) => new[] { (float)i, 1f }).ToList());
        }
    }

    private sealed class FailingBatchEmbeddingClient : IBatchEmbeddingClient
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct) => throw new InvalidOperationException("single path should not be used");
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct) => throw new InvalidOperationException("provider failed before publication");
    }

    private sealed class RecordingVectorStore : IVectorStore
    {
        public int UpsertCalls { get; private set; }
        public int LastCount { get; private set; }

        public Task UpsertAsync(IReadOnlyList<(RagChunk Chunk, float[] Embedding)> items, CancellationToken ct)
        {
            UpsertCalls++;
            LastCount = items.Count;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RagRetrievedChunk>> QueryAsync(Guid scopeId, float[] embedding, int topK, IReadOnlyDictionary<string, string>? filters, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RagRetrievedChunk>>(Array.Empty<RagRetrievedChunk>());
    }
}
