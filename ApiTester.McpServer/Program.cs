using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    // Critical: MCP stdio uses stdout for protocol messages.
    // Send logs to stderr, or you will corrupt the JSON-RPC stream.
    o.LogToStandardErrorThreshold = LogLevel.Information;
});

// Core services
builder.Services.AddSingleton(AppConfig.Load());
builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<ApiRuntimeConfig>();
builder.Services.AddSingleton<SsrfGuard>();
builder.Services.AddSingleton<ApiAssistTools>();
builder.Services.AddSingleton<TestPlanRunner>();

// Day 4: policy + guardrails tools (DI registrations)
builder.Services.AddSingleton<PolicyTools>();

// HTTP execution
builder.Services.AddHttpClient();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
