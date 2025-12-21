using ApiTester.McpServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    // Critical, MCP stdio uses stdout for protocol messages.
    // Send logs to stderr, or you will corrupt the JSON-RPC stream.
    o.LogToStandardErrorThreshold = LogLevel.Information;
});

builder.Services.AddSingleton(AppConfig.Load());
builder.Services.AddSingleton<OpenApiStore>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();


await builder.Build().RunAsync();
