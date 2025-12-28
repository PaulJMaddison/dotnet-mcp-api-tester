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
builder.Services.AddSingleton<TestPlanRunner>();

// HTTP execution
builder.Services.AddHttpClient();

// Always register file store (fallback/default)
builder.Services.AddSingleton<FileTestRunStore>();

// Conditionally register DbContext + SQL stores if configured
builder.Services.AddDbContext<ApiTesterDbContext>((sp, opt) =>
{
    var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var provider = (p.Provider ?? "File").Trim();
    var cs = (p.ConnectionString ?? "").Trim();

    // Only configure EF when SqlServer + cs is present
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs))
    {
        opt.UseSqlServer(cs);
    }
});

// Register SQL stores (scoped, because DbContext is scoped)
builder.Services.AddScoped<SqlRunStore>();
builder.Services.AddScoped<SqlProjectStore>();
builder.Services.AddSingleton<ProjectContext>();
builder.Services.AddScoped<IProjectStore, SqlProjectStore>();
builder.Services.AddScoped<IRunStore, SqlRunStore>();

// Choose ITestRunStore based on PersistenceOptions
builder.Services.AddSingleton<ITestRunStore>(sp =>
{
    var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var provider = (p.Provider ?? "File").Trim();
    var cs = (p.ConnectionString ?? "").Trim();

    // Default path: file store
    if (!provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<FileTestRunStore>();

    // SqlServer selected but not configured, fall back to file (don’t crash local dev)
    if (string.IsNullOrWhiteSpace(cs))
        return sp.GetRequiredService<FileTestRunStore>();

    // IMPORTANT: don't new SqlRunStore(cs) — it needs DbContext.
    // If you haven't built a Sql-backed ITestRunStore yet, keep file store for now.
    return sp.GetRequiredService<FileTestRunStore>();
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
