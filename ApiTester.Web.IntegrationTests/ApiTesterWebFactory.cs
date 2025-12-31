using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Web.IntegrationTests;

public sealed class ApiTesterWebFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"apitester-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Sqlite",
                ["Persistence:ConnectionString"] = $"Data Source={_databasePath}",
                ["Execution:AllowedBaseUrls:0"] = "https://httpbin.org",
                ["Execution:DryRun"] = "false"
            };
            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApiTesterDbContext>();
            services.RemoveAll<ITestRunStore>();
            services.RemoveAll<IProjectStore>();
            services.RemoveAll<IOpenApiSpecStore>();
            services.RemoveAll<ITestPlanStore>();

            services.AddDbContext<ApiTesterDbContext>(opt => opt.UseSqlite($"Data Source={_databasePath}"));
            services.AddScoped<SqlTestRunStore>();
            services.AddScoped<SqlProjectStore>();
            services.AddScoped<SqlOpenApiSpecStore>();
            services.AddScoped<SqlTestPlanStore>();
            services.AddScoped<ITestRunStore>(sp => sp.GetRequiredService<SqlTestRunStore>());
            services.AddScoped<IProjectStore>(sp => sp.GetRequiredService<SqlProjectStore>());
            services.AddScoped<IOpenApiSpecStore>(sp => sp.GetRequiredService<SqlOpenApiSpecStore>());
            services.AddScoped<ITestPlanStore>(sp => sp.GetRequiredService<SqlTestPlanStore>());

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
            File.Delete(_databasePath);
    }
}
