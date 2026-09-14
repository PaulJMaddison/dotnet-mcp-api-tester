using ApiTester.AI;
using ApiTester.AI.Azure;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Information);
builder.Services.AddSingleton(McpSafetyOptions.FromConfiguration(builder.Configuration));

var azure = new AzureOpenAiOptions
{
    Endpoint = FirstNonEmpty(builder.Configuration["AzureOpenAI:Endpoint"], Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
    ChatDeployment = FirstNonEmpty(builder.Configuration["AzureOpenAI:ChatDeployment"], Environment.GetEnvironmentVariable("AZURE_OPENAI_CHAT_DEPLOYMENT")),
    EmbeddingDeployment = FirstNonEmpty(builder.Configuration["AzureOpenAI:EmbeddingDeployment"], Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT")),
    Authentication = FirstNonEmpty(builder.Configuration["AzureOpenAI:Authentication"], Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTHENTICATION")),
    CredentialSource = FirstNonEmpty(builder.Configuration["AzureOpenAI:CredentialSource"], Environment.GetEnvironmentVariable("AZURE_OPENAI_CREDENTIAL_SOURCE")),
    ApiKey = FirstNonEmpty(builder.Configuration["AzureOpenAI:ApiKey"], Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")),
    BearerToken = FirstNonEmpty(builder.Configuration["AzureOpenAI:BearerToken"], Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTH_TOKEN")),
    TimeoutSeconds = builder.Configuration.GetValue<int?>("AzureOpenAI:TimeoutSeconds") ?? 30,
    MaxRetries = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxRetries") ?? 2,
    MaxResponseBytes = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxResponseBytes") ?? 1_048_576,
    MaxInputChars = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxInputChars") ?? 120_000,
    MaxCompletionTokens = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxCompletionTokens") ?? 0,
    CircuitBreakerFailureThreshold = builder.Configuration.GetValue<int?>("AzureOpenAI:CircuitBreakerFailureThreshold") ?? 4,
    CircuitBreakerBreakSeconds = builder.Configuration.GetValue<int?>("AzureOpenAI:CircuitBreakerBreakSeconds") ?? 30
};

azure.ValidateChat();
azure.ValidateEmbedding();
builder.Services.AddSingleton(azure);

builder.Services.AddHttpClient("AzureOpenAI", client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton(sp => new AzureOpenAiTransport(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("AzureOpenAI"),
    sp.GetRequiredService<AzureOpenAiOptions>()));

builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<ApiRuntimeConfig>();
builder.Services.AddSingleton<SsrfGuard>();
builder.Services.AddSingleton<InMemoryVectorStore>();
builder.Services.AddSingleton<OpenApiEvidenceBuilder>();
builder.Services.AddSingleton<OpenApiConstraintTestGenerator>();

builder.Services.AddSingleton<IEmbeddingClient>(sp => new AzureOpenAiEmbeddingClient(
    sp.GetRequiredService<AzureOpenAiTransport>(),
    sp.GetRequiredService<AzureOpenAiOptions>()));

builder.Services.AddSingleton<IAiClient>(sp => new AzureOpenAiClient(
    sp.GetRequiredService<AzureOpenAiTransport>(),
    sp.GetRequiredService<AzureOpenAiOptions>()));

builder.Services.AddSingleton<IChatCompletionClient, AiClientChatCompletionClient>();
builder.Services.AddSingleton<RagRuntime>();

builder.Services.AddHttpClient(ExecuteTools.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false
    });
builder.Services.AddHttpClient();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string FirstNonEmpty(params string?[] values) =>
    values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
