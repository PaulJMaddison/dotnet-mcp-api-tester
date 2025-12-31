using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Services;
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

// IMPORTANT: scoped because it uses ITestRunStore which may be SQL (DbContext scoped)
builder.Services.AddScoped<TestPlanRunner>();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<ProjectContext>();
builder.Services.AddApiTesterPersistence(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
