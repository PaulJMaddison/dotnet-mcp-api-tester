using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
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

// Core services
builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<ApiRuntimeConfig>();
builder.Services.AddSingleton<SsrfGuard>();
builder.Services.AddSingleton<TestPlanRunner>();
builder.Services.AddSingleton<ITestRunStore, FileTestRunStore>();

// Tool registration is handled by WithToolsFromAssembly(), so you don't need these.
// Keep them out unless a tool has a special factory/lifetime requirement.
// builder.Services.AddSingleton<ApiAssistTools>();
// builder.Services.AddSingleton<PolicyTools>();

// HTTP execution
builder.Services.AddHttpClient();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
