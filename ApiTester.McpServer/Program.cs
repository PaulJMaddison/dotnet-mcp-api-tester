using ApiTester.AI;
using ApiTester.AI.Azure;
using ApiTester.AI.Local;
using ApiTester.McpServer.Evals;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Services;
using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    // MCP stdio uses stdout for protocol messages.
    // Send logs to stderr or you'll corrupt the JSON-RPC stream.
    o.LogToStandardErrorThreshold = LogLevel.Information;
});

var appConfig = AppConfig.Load(builder.Configuration);
builder.Services.AddSingleton(appConfig);

// Core services
builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<ApiRuntimeConfig>();
builder.Services.AddSingleton<SsrfGuard>();
builder.Services.AddSingleton<EvalRunner>();
builder.Services.AddSingleton<ProjectContext>();
builder.Services.AddSingleton<InMemoryVectorStore>();
builder.Services.AddSingleton<IEmbeddingClient>(_ => new DeterministicHashEmbeddingClient(256));

builder.Services.AddSingleton<IAiClient>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();

    var endpoint = cfg["AzureOpenAI:Endpoint"];
    var deployment = cfg["AzureOpenAI:ChatDeployment"];

    if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deployment))
    {
        return new AzureOpenAiClient();
    }

    return new LocalGroundedAiClient();
});


builder.Services.AddSingleton<IChatCompletionClient, AiClientChatCompletionClient>();
builder.Services.AddSingleton<RagRuntime>();


// IMPORTANT: scoped because it uses ITestRunStore which may be SQL (DbContext scoped)
builder.Services.AddScoped<TestPlanRunner>();

builder.Services.AddHttpClient();

builder.Services.AddApiTesterPersistence(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
