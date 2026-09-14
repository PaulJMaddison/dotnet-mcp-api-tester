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
using System.Net.Http;
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

var azureOpenAi = new AzureOpenAiOptions
{
    Endpoint = FirstNonEmpty(
        builder.Configuration["AzureOpenAI:Endpoint"],
        Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
    ChatDeployment = FirstNonEmpty(
        builder.Configuration["AzureOpenAI:ChatDeployment"],
        Environment.GetEnvironmentVariable("AZURE_OPENAI_CHAT_DEPLOYMENT")),
    EmbeddingDeployment = FirstNonEmpty(
        builder.Configuration["AzureOpenAI:EmbeddingDeployment"],
        Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT")),
    ApiKey = FirstNonEmpty(
        builder.Configuration["AzureOpenAI:ApiKey"],
        Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")),
    BearerToken = FirstNonEmpty(
        builder.Configuration["AzureOpenAI:BearerToken"],
        Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTH_TOKEN")),
    TimeoutSeconds = builder.Configuration.GetValue<int?>("AzureOpenAI:TimeoutSeconds") ?? 30,
    MaxRetries = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxRetries") ?? 2,
    MaxResponseBytes = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxResponseBytes") ?? 1_048_576,
    MaxInputChars = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxInputChars") ?? 120_000,
    MaxCompletionTokens = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxCompletionTokens") ?? 1_500,
    CircuitBreakerFailureThreshold = builder.Configuration.GetValue<int?>("AzureOpenAI:CircuitBreakerFailureThreshold") ?? 4,
    CircuitBreakerBreakSeconds = builder.Configuration.GetValue<int?>("AzureOpenAI:CircuitBreakerBreakSeconds") ?? 30
};
builder.Services.AddSingleton(azureOpenAi);

builder.Services.AddHttpClient("AzureOpenAI", client =>
{
    // Per-request timeout is controlled by AzureOpenAiTransport so cancellation
    // remains explicit and retry attempts get a fresh timeout window.
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton(sp => new AzureOpenAiTransport(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("AzureOpenAI"),
    sp.GetRequiredService<AzureOpenAiOptions>()));

// Core services
builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<ApiRuntimeConfig>();
builder.Services.AddSingleton<SsrfGuard>();
builder.Services.AddSingleton<EvalRunner>();
builder.Services.AddSingleton<ProjectContext>();
builder.Services.AddSingleton<InMemoryVectorStore>();

builder.Services.AddSingleton<IEmbeddingClient>(sp =>
{
    var options = sp.GetRequiredService<AzureOpenAiOptions>();
    if (options.IsEmbeddingConfigured)
    {
        return new AzureOpenAiEmbeddingClient(
            sp.GetRequiredService<AzureOpenAiTransport>(),
            options);
    }

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ApiTester.Rag.Embeddings");
    if (!string.IsNullOrWhiteSpace(options.Endpoint) || !string.IsNullOrWhiteSpace(options.EmbeddingDeployment))
    {
        logger.LogWarning(
            "Azure OpenAI embeddings are only partially configured; using deterministic lexical feature hashing. " +
            "Set Endpoint, EmbeddingDeployment and credentials to enable real embeddings.");
    }
    else
    {
        logger.LogInformation(
            "Azure OpenAI embeddings are not configured; using deterministic lexical feature hashing for local/offline RAG.");
    }

    return new DeterministicHashEmbeddingClient(512);
});

builder.Services.AddSingleton<IAiClient>(sp =>
{
    var options = sp.GetRequiredService<AzureOpenAiOptions>();
    if (options.IsChatConfigured)
    {
        return new AzureOpenAiClient(
            sp.GetRequiredService<AzureOpenAiTransport>(),
            options);
    }

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ApiTester.AI");
    if (!string.IsNullOrWhiteSpace(options.Endpoint) || !string.IsNullOrWhiteSpace(options.ChatDeployment))
    {
        logger.LogWarning(
            "Azure OpenAI chat is only partially configured; using the local grounded client. " +
            "Set Endpoint, ChatDeployment and credentials to enable Azure chat.");
    }
    else
    {
        logger.LogInformation("Azure OpenAI chat is not configured; using the local grounded client.");
    }

    return new LocalGroundedAiClient();
});

builder.Services.AddSingleton<IChatCompletionClient, AiClientChatCompletionClient>();
builder.Services.AddSingleton<RagRuntime>();

// IMPORTANT: scoped because it uses ITestRunStore which may be SQL (DbContext scoped)
builder.Services.AddScoped<TestPlanRunner>();

builder.Services.AddHttpClient(TestPlanRunner.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false
    });
builder.Services.AddHttpClient();

builder.Services.AddApiTesterPersistence(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string FirstNonEmpty(params string?[] values) =>
    values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
