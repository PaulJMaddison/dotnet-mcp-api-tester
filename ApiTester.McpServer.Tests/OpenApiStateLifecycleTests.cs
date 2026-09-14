using System.Net;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Models;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class OpenApiStateLifecycleTests
{
    [Fact]
    public void ExplicitOperationIdIsTrimmedAndMissingIdsAreUniqueByMethodAndPath()
    {
        var doc = Parse("""
        {
          "openapi":"3.0.1","info":{"title":"Ids","version":"1"},
          "paths":{"/items":{"get":{"operationId":"  customGet  ","responses":{"200":{"description":"OK"}}},"post":{"responses":{"201":{"description":"Created"}}}},"/other":{"post":{"responses":{"201":{"description":"Created"}}}}}
        }
        """);

        OpenApiOperationIdentity.EnsureOperationIds(doc);

        Assert.NotNull(OpenApiOperationIdentity.Find(doc, "customGet"));
        Assert.NotNull(OpenApiOperationIdentity.Find(doc, "CUSTOMGET"));
        Assert.NotNull(OpenApiOperationIdentity.Find(doc, "Post:/items"));
        Assert.NotNull(OpenApiOperationIdentity.Find(doc, "Post:/other"));
        Assert.Null(OpenApiOperationIdentity.Find(doc, "Get:/other"));
    }

    [Fact]
    public void StoreRejectsInvalidIdentifiersAndEmptyHash()
    {
        var store = new OpenApiStore();
        var doc = Parse("""{"openapi":"3.0.1","info":{"title":"A","version":"1"},"paths":{}}""");

        Assert.Throws<ArgumentException>(() => store.SetDocument(Guid.Empty, Guid.NewGuid(), doc, "a", "hash", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => store.SetDocument(Guid.NewGuid(), Guid.Empty, doc, "a", "hash", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => store.SetDocument(Guid.NewGuid(), Guid.NewGuid(), doc, "a", " ", DateTime.UtcNow));
    }

    [Fact]
    public async Task SuccessfulReloadReplacesContractAndDeletesOldDerivedVectorScope()
    {
        var store = new OpenApiStore();
        var vectors = new InMemoryVectorStore();
        var oldScope = Guid.NewGuid();
        var oldSource = Guid.NewGuid();
        store.SetDocument(oldScope, oldSource, Parse("""{"openapi":"3.0.1","info":{"title":"Old","version":"1"},"paths":{"/old":{"get":{"operationId":"old","responses":{"200":{"description":"OK"}}}}}}"""), "old", "hash", DateTime.UtcNow);
        await vectors.UpsertAsync(new[]
        {
            (new RagChunk(oldScope, "openapi", oldSource.ToString(), "old-chunk", "old evidence", "old-hash", DateTime.UtcNow, new Dictionary<string,string>{{"EvidenceType","operation"}}), new[] { 1f, 0f })
        }, CancellationToken.None);

        var tools = CreateTools(store, vectors);
        var file = await TempSpecAsync("""
        {"openapi":"3.0.1","info":{"title":"New","version":"1"},"paths":{"/new":{"get":{"operationId":"new","responses":{"200":{"description":"OK"}}}}}}
        """);

        try
        {
            await tools.ApiLoadOpenApi(file);
            var current = store.RequireSnapshot();

            Assert.NotEqual(oldScope, current.ScopeId);
            Assert.Equal("New", current.Document.Info.Title);
            Assert.Empty(await vectors.QueryAsync(oldScope, new[] { 1f, 0f }, 10, null, CancellationToken.None));
            Assert.NotEmpty(await vectors.QueryAsync(current.ScopeId, new[] { 1f, 0f }, 10, null, CancellationToken.None));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task MalformedReloadDoesNotReplaceCurrentContract()
    {
        var store = new OpenApiStore();
        var oldScope = Guid.NewGuid();
        store.SetDocument(oldScope, Guid.NewGuid(), Parse("""{"openapi":"3.0.1","info":{"title":"Old","version":"1"},"paths":{"/old":{"get":{"operationId":"old","responses":{"200":{"description":"OK"}}}}}}"""), "old", "hash", DateTime.UtcNow);
        var tools = CreateTools(store, new InMemoryVectorStore());
        var file = await TempSpecAsync("this is not openapi");

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => tools.ApiLoadOpenApi(file));
            Assert.Equal(oldScope, store.RequireSnapshot().ScopeId);
            Assert.Equal("Old", store.RequireDocument().Info.Title);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static OpenApiTools CreateTools(OpenApiStore store, InMemoryVectorStore vectors)
    {
        var embedding = new ConstantEmbedding();
        return new OpenApiTools(
            store,
            new ApiRuntimeConfig(),
            new StubFactory(),
            new SsrfGuard(),
            new OpenApiEvidenceBuilder(),
            new RagRuntime(new NoChat(), embedding, vectors),
            vectors,
            NullLogger<OpenApiTools>.Instance);
    }

    private static Microsoft.OpenApi.Models.OpenApiDocument Parse(string text)
    {
        var doc = new OpenApiStringReader().Read(text, out var diagnostics);
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

    private sealed class ConstantEmbedding : IBatchEmbeddingClient
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct) => Task.FromResult(new[] { 1f, 0f });
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new[] { 1f, 0f }).ToList());
    }

    private sealed class NoChat : IChatCompletionClient
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
            => throw new InvalidOperationException("chat not expected");
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler());
        private sealed class StubHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
