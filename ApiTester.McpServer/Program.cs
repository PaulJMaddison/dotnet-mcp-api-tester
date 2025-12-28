using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.Diagnostics;



var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    o.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Information;
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

// Always available file store fallback
builder.Services.AddSingleton<FileTestRunStore>();

// Decide persistence
var persistence = builder.Configuration.GetSection("Persistence").Get<PersistenceOptions>() ?? new PersistenceOptions();
var provider = (persistence.Provider ?? "File").Trim();
var cs = (persistence.ConnectionString ?? "").Trim();

var sqlEnabled = provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs);

if (sqlEnabled)
{
    builder.Services.AddDbContext<ApiTesterDbContext>(opt => opt.UseSqlServer(cs));

    // SQL backed store for runs
    builder.Services.AddScoped<SqlTestRunStore>();
    builder.Services.AddScoped<ITestRunStore, SqlTestRunStore>();
}
else
{
    // File backed store
    builder.Services.AddSingleton<ITestRunStore>(sp => sp.GetRequiredService<FileTestRunStore>());
}

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();