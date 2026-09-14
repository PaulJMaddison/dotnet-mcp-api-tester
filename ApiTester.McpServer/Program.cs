using ApiTester.AI;
using ApiTester.AI.Azure;
using ApiTester.AI.Local;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.VectorStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    o.LogToStandardErrorThreshold = LogLevel.Information;
});

builder.Services.AddSingleton(AppConfig.Load());
builder.Services.AddSingleton(McpSafetyOptions.FromConfiguration(builder.Configuration));

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
    Authentication = FirstNonEmpty(
        builder.Configuration["AzureOpenAI:Authentication"],
        Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTHENTICATION")),
    CredentialSource = FirstNonEmpty(
        builder.Configuration["AzureOpenAI:CredentialSource"],
        Environment.GetEnvironmentVariable("AZURE_OPENAI_CREDENTIAL_SOURCE")),
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
    MaxCompletionTokens = builder.Configuration.GetValue<int?>("AzureOpenAI:MaxCompletionTokens") ?? 0,
    CircuitBreakerFailureThreshold = builder.Configuration.GetValue<int?>("AzureOpenAI:CircuitBreakerFailureThreshold") ?? 4,
    CircuitBreakerBreakSeconds = builder.Configuration.GetValue<int?>("AzureOpenAI:CircuitBreakerBreakSeconds") ?? 30
};
builder.Services.AddSingleton(azureOpenAi);

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

builder.Services.AddSingleton<IEmbeddingClient>(sp =>
{
    var options = sp.GetRequiredService<AzureOpenAiOptions>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ApiTester.Rag.Embeddings");

    if (options.IsEmbeddingConfigured)
    {
        logger.LogInformation(
            "Using Azure OpenAI embedding deployment {EmbeddingDeployment} with {AuthenticationMode}/{CredentialSource}.",
            options.EmbeddingDeployment,
            options.GetAuthenticationMode(),
            options.GetAuthenticationMode() == AzureOpenAiAuthenticationMode.DefaultAzureCredential
                ? options.GetCredentialSource()
                : AzureOpenAiCredentialSource.Default);

        return new AzureOpenAiEmbeddingClient(
            sp.GetRequiredService<AzureOpenAiTransport>(),
            options);
    }

    logger.LogInformation("Azure OpenAI embeddings are not configured; using deterministic lexical feature hashing for local/offline RAG.");
    return new DeterministicHashEmbeddingClient(512);
});

builder.Services.AddSingleton<IAiClient>(sp =>
{
    var options = sp.GetRequiredService<AzureOpenAiOptions>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ApiTester.AI");

    if (options.IsChatConfigured)
    {
        logger.LogInformation(
            "Using Azure OpenAI chat deployment {ChatDeployment} with {AuthenticationMode}/{CredentialSource}.",
            options.ChatDeployment,
            options.GetAuthenticationMode(),
            options.GetAuthenticationMode() == AzureOpenAiAuthenticationMode.DefaultAzureCredential
                ? options.GetCredentialSource()
                : AzureOpenAiCredentialSource.Default);

        return new AzureOpenAiClient(
            sp.GetRequiredService<AzureOpenAiTransport>(),
            options);
    }

    logger.LogInformation("Azure OpenAI chat is not configured; using the local grounded client.");
    return new LocalGroundedAiClient();
});

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
