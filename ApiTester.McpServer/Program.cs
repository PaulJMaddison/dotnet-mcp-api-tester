using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

// Options
builder.Services.Configure<PersistenceOptions>(builder.Configuration.GetSection("Persistence"));

// Core services
builder.Services.AddSingleton<OpenApiStore>();
builder.Services.AddSingleton<ApiRuntimeConfig>();
builder.Services.AddSingleton<SsrfGuard>();

// IMPORTANT: scoped because it uses ITestRunStore which may be SQL (DbContext scoped)
builder.Services.AddScoped<TestPlanRunner>();

builder.Services.AddHttpClient();

// Always register file store (safe fallback)
builder.Services.AddSingleton<FileTestRunStore>();

// DbContext (only configured when SqlServer + cs is present)
builder.Services.AddDbContext<ApiTesterDbContext>((sp, opt) =>
{
    var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var provider = (p.Provider ?? "File").Trim();
    var cs = (p.ConnectionString ?? "").Trim();

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs))
        opt.UseSqlServer(cs);
});

// SQL stores (scoped)
builder.Services.AddScoped<SqlTestRunStore>();
builder.Services.AddScoped<SqlProjectStore>();

builder.Services.AddSingleton<ProjectContext>();

// Choose ITestRunStore (scoped)
builder.Services.AddScoped<ITestRunStore>(sp =>
{
    var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var provider = (p.Provider ?? "File").Trim();
    var cs = (p.ConnectionString ?? "").Trim();

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs))
        return sp.GetRequiredService<SqlTestRunStore>();

    return sp.GetRequiredService<FileTestRunStore>();
});

// Choose IProjectStore (scoped)
builder.Services.AddScoped<IProjectStore>(sp =>
{
    var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var provider = (p.Provider ?? "File").Trim();
    var cs = (p.ConnectionString ?? "").Trim();

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs))
        return sp.GetRequiredService<SqlProjectStore>();

    // If you don't have a FileProjectStore yet, do NOT expose project tools in File mode.
    // For now, throw a clear error.
    throw new InvalidOperationException("Project store is only available when Persistence.Provider=SqlServer and a ConnectionString is configured.");
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
