using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Tools;
using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class RagToolsContextTests
{
    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        var runtime = Runtime();
        var context = new ProjectContext();
        var specs = new FakeOpenApiSpecStore();
        var logger = NullLogger<RagTools>.Instance;

        Assert.Throws<ArgumentNullException>(() => new RagTools(null!, context, specs, logger));
        Assert.Throws<ArgumentNullException>(() => new RagTools(runtime, null!, specs, logger));
        Assert.Throws<ArgumentNullException>(() => new RagTools(runtime, context, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new RagTools(runtime, context, specs, null!));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Index_InvalidProjectId_ReturnsOkFalseAndDoesNotChangeContext(string projectId)
    {
        var context = new ProjectContext();
        var existing = Guid.NewGuid();
        context.SetCurrentProject(existing);
        var tools = Tools(context: context);

        var result = await tools.ApiRagIndexProject(projectId, CancellationToken.None);
        using var json = ToJson(result);

        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(existing, context.CurrentProjectId);
    }

    [Fact]
    public async Task Index_NoExplicitOrCurrentProject_ReturnsHelpfulFailure()
    {
        var tools = Tools(context: new ProjectContext());

        var result = await tools.ApiRagIndexProject(null, CancellationToken.None);
        using var json = ToJson(result);

        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("No current project", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Index_ExplicitProjectBecomesCurrentContext()
    {
        var projectId = Guid.NewGuid();
        var context = new ProjectContext();
        var specs = new FakeOpenApiSpecStore();
        specs.Set(projectId, Spec(projectId, Guid.NewGuid(), "Customer API", SmallSpec("/customers", "getCustomers")));
        var tools = Tools(context: context, specs: specs);

        var result = await tools.ApiRagIndexProject(projectId.ToString(), CancellationToken.None);
        using var json = ToJson(result);

        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(projectId, context.CurrentProjectId);
        Assert.Equal(projectId, json.RootElement.GetProperty("projectId").GetGuid());
    }

    [Fact]
    public async Task Index_SmallOpenApiSpecProducesContextChunk()
    {
        var projectId = Guid.NewGuid();
        var specs = new FakeOpenApiSpecStore();
        specs.Set(projectId, Spec(projectId, Guid.NewGuid(), "Tiny API", SmallSpec("/ping", "ping")));
        var tools = Tools(specs: specs);

        var result = await tools.ApiRagIndexProject(projectId.ToString(), CancellationToken.None);
        using var json = ToJson(result);

        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("specCount").GetInt32());
        Assert.True(json.RootElement.GetProperty("indexedChunks").GetInt32() >= 1);
    }

    [Fact]
    public async Task Index_NoSpecs_IsSuccessfulNoOp()
    {
        var projectId = Guid.NewGuid();
        var tools = Tools(specs: new FakeOpenApiSpecStore());

        var result = await tools.ApiRagIndexProject(projectId.ToString(), CancellationToken.None);
        using var json = ToJson(result);

        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("specCount").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("indexedChunks").GetInt32());
    }

    [Fact]
    public async Task Index_StoreReturningDifferentProjectFailsClosed()
    {
        var requestedProject = Guid.NewGuid();
        var otherProject = Guid.NewGuid();
        var specs = new FakeOpenApiSpecStore
        {
            OverrideList = new[] { Spec(otherProject, Guid.NewGuid(), "Wrong project", SmallSpec("/wrong", "wrong")) }
        };
        var tools = Tools(specs: specs);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.ApiRagIndexProject(requestedProject.ToString(), CancellationToken.None));

        Assert.Contains("different project context", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_StoreReturningDifferentTenantFailsClosed()
    {
        var projectId = Guid.NewGuid();
        var wrongTenant = Guid.NewGuid();
        var spec = new OpenApiSpecRecord(
            Guid.NewGuid(),
            projectId,
            wrongTenant,
            "Wrong tenant",
            "1.0",
            SmallSpec("/wrong", "wrong"),
            "hash",
            DateTime.UtcNow);
        var specs = new FakeOpenApiSpecStore { OverrideList = new[] { spec } };
        var tools = Tools(specs: specs);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.ApiRagIndexProject(projectId.ToString(), CancellationToken.None));

        Assert.Contains("different tenant context", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ask_BlankQuestionReturnsOkFalse(string? question)
    {
        var context = new ProjectContext();
        context.SetCurrentProject(Guid.NewGuid());
        var tools = Tools(context: context);

        var result = await tools.ApiRagAsk(question!, 10, null, CancellationToken.None);
        using var json = ToJson(result);

        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("Question is required", json.RootElement.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Ask_InvalidExplicitProjectDoesNotOverwriteCurrentContext(string projectId)
    {
        var context = new ProjectContext();
        var current = Guid.NewGuid();
        context.SetCurrentProject(current);
        var tools = Tools(context: context);

        var result = await tools.ApiRagAsk("question", 10, projectId, CancellationToken.None);
        using var json = ToJson(result);

        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(current, context.CurrentProjectId);
    }

    [Fact]
    public async Task Ask_NoProjectContextReturnsOkFalse()
    {
        var tools = Tools(context: new ProjectContext());

        var result = await tools.ApiRagAsk("question", 10, null, CancellationToken.None);
        using var json = ToJson(result);

        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("No current project", json.RootElement.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public async Task Ask_TopKBoundaryValuesDoNotEscapeRagClamp(int topK)
    {
        var projectId = Guid.NewGuid();
        var specs = new FakeOpenApiSpecStore();
        specs.Set(projectId, Spec(projectId, Guid.NewGuid(), "Customer API", SmallSpec("/customers", "getCustomers")));
        var tools = Tools(specs: specs);
        await tools.ApiRagIndexProject(projectId.ToString(), CancellationToken.None);

        var result = await tools.ApiRagAsk("customers", topK, projectId.ToString(), CancellationToken.None);
        using var json = ToJson(result);

        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(json.RootElement.GetProperty("evidence").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Ask_ExplicitProjectSwitchesContextAndReturnsThatProjectOnly()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var specA = Guid.NewGuid();
        var specB = Guid.NewGuid();
        var specs = new FakeOpenApiSpecStore();
        specs.Set(projectA, Spec(projectA, specA, "Customer API", SmallSpec("/customers", "getCustomers")));
        specs.Set(projectB, Spec(projectB, specB, "Weather API", SmallSpec("/weather", "getWeather")));
        var context = new ProjectContext();
        var tools = Tools(context: context, specs: specs);

        await tools.ApiRagIndexProject(projectA.ToString(), CancellationToken.None);
        await tools.ApiRagIndexProject(projectB.ToString(), CancellationToken.None);

        var result = await tools.ApiRagAsk("customer endpoint", 10, projectA.ToString(), CancellationToken.None);
        using var json = ToJson(result);

        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(projectA, context.CurrentProjectId);
        Assert.Equal(projectA, json.RootElement.GetProperty("projectId").GetGuid());

        var evidence = json.RootElement.GetProperty("evidence").EnumerateArray().ToList();
        Assert.NotEmpty(evidence);
        Assert.All(evidence, item => Assert.Equal(specA.ToString(), item.GetProperty("sourceId").GetString()));
        Assert.DoesNotContain(evidence, item => item.GetProperty("sourceId").GetString() == specB.ToString());
    }

    [Fact]
    public async Task Ask_TrimsQuestionBeforeItReachesModel()
    {
        var projectId = Guid.NewGuid();
        var specs = new FakeOpenApiSpecStore();
        specs.Set(projectId, Spec(projectId, Guid.NewGuid(), "Customer API", SmallSpec("/customers", "getCustomers")));
        var chat = new RecordingChatClient();
        var tools = Tools(specs: specs, chat: chat);
        await tools.ApiRagIndexProject(projectId.ToString(), CancellationToken.None);

        await tools.ApiRagAsk("   list customers   ", 10, projectId.ToString(), CancellationToken.None);

        Assert.Contains("list customers", chat.LastUserPrompt);
        Assert.DoesNotContain("   list customers   ", chat.LastUserPrompt);
    }

    [Fact]
    public async Task Index_PreCancelledTokenDoesNotReadStore()
    {
        var specs = new FakeOpenApiSpecStore();
        var tools = Tools(specs: specs);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            tools.ApiRagIndexProject(Guid.NewGuid().ToString(), cts.Token));

        Assert.Equal(0, specs.ListCalls);
    }

    [Fact]
    public async Task Ask_PreCancelledTokenDoesNotCallModel()
    {
        var context = new ProjectContext();
        context.SetCurrentProject(Guid.NewGuid());
        var chat = new RecordingChatClient();
        var tools = Tools(context: context, chat: chat);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            tools.ApiRagAsk("question", 10, null, cts.Token));

        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public void ProjectContext_RejectsEmptyGuidAndClearRemovesContext()
    {
        var context = new ProjectContext();
        Assert.Throws<ArgumentException>(() => context.SetCurrentProject(Guid.Empty));

        var projectId = Guid.NewGuid();
        context.SetCurrentProject(projectId);
        Assert.Equal(projectId, context.CurrentProjectId);

        context.Clear();
        Assert.Null(context.CurrentProjectId);
    }

    private static RagTools Tools(
        ProjectContext? context = null,
        FakeOpenApiSpecStore? specs = null,
        RecordingChatClient? chat = null)
    {
        var vectorStore = new InMemoryVectorStore();
        var embeddings = new DeterministicHashEmbeddingClient(512);
        var runtime = new RagRuntime(chat ?? new RecordingChatClient(), embeddings, vectorStore);
        return new RagTools(
            runtime,
            context ?? new ProjectContext(),
            specs ?? new FakeOpenApiSpecStore(),
            NullLogger<RagTools>.Instance);
    }

    private static RagRuntime Runtime() => new(
        new RecordingChatClient(),
        new DeterministicHashEmbeddingClient(512),
        new InMemoryVectorStore());

    private static OpenApiSpecRecord Spec(Guid projectId, Guid specId, string title, string json) =>
        new(
            specId,
            projectId,
            OrgDefaults.DefaultOrganisationId,
            title,
            "1.0",
            json,
            specId.ToString("N"),
            DateTime.UtcNow);

    private static string SmallSpec(string path, string operationId) => $$"""
    {
      "openapi": "3.0.1",
      "info": { "title": "Test API", "version": "1.0" },
      "paths": {
        "{{path}}": {
          "get": {
            "operationId": "{{operationId}}",
            "responses": { "200": { "description": "OK" } }
          }
        }
      }
    }
    """;

    private static JsonDocument ToJson(object value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value));

    private sealed class RecordingChatClient : IChatCompletionClient
    {
        public int CallCount { get; private set; }
        public string LastUserPrompt { get; private set; } = string.Empty;

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            LastUserPrompt = userPrompt;
            return Task.FromResult("grounded answer");
        }
    }

    private sealed class FakeOpenApiSpecStore : IOpenApiSpecStore
    {
        private readonly Dictionary<Guid, List<OpenApiSpecRecord>> _byProject = new();

        public IReadOnlyList<OpenApiSpecRecord>? OverrideList { get; init; }
        public int ListCalls { get; private set; }

        public void Set(Guid projectId, params OpenApiSpecRecord[] records) =>
            _byProject[projectId] = records.ToList();

        public Task<OpenApiSpecRecord?> GetAsync(Guid tenantId, Guid projectId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _byProject.TryGetValue(projectId, out var records);
            return Task.FromResult(records?.FirstOrDefault());
        }

        public Task<IReadOnlyList<OpenApiSpecRecord>> ListAsync(Guid tenantId, Guid projectId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ListCalls++;
            if (OverrideList is not null)
                return Task.FromResult(OverrideList);

            _byProject.TryGetValue(projectId, out var records);
            return Task.FromResult<IReadOnlyList<OpenApiSpecRecord>>(records ?? new List<OpenApiSpecRecord>());
        }

        public Task<OpenApiSpecRecord?> GetByIdAsync(Guid tenantId, Guid specId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var result = _byProject.Values.SelectMany(x => x).FirstOrDefault(x => x.SpecId == specId);
            return Task.FromResult(result);
        }

        public Task<OpenApiSpecRecord> UpsertAsync(
            Guid tenantId,
            Guid projectId,
            string title,
            string version,
            string specJson,
            string specHash,
            DateTime createdUtc,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var record = new OpenApiSpecRecord(
                Guid.NewGuid(), projectId, tenantId, title, version, specJson, specHash, createdUtc);
            if (!_byProject.TryGetValue(projectId, out var records))
            {
                records = new List<OpenApiSpecRecord>();
                _byProject[projectId] = records;
            }
            records.Add(record);
            return Task.FromResult(record);
        }

        public Task<bool> DeleteAsync(Guid tenantId, Guid specId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var records in _byProject.Values)
            {
                var index = records.FindIndex(x => x.SpecId == specId);
                if (index >= 0)
                {
                    records.RemoveAt(index);
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }
    }
}
