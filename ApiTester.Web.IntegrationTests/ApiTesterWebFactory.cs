using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Web.IntegrationTests;

public sealed class ApiTesterWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Cache=Shared");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            _connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Sqlite",
                ["Persistence:ConnectionString"] = "Data Source=:memory:;Cache=Shared",
                ["Execution:AllowedBaseUrls:0"] = "https://httpbin.org",
                ["Execution:DryRun"] = "false",
                ["Auth:ApiKey"] = ApiKeyAlpha,
                ["Auth:ApiKeys:0"] = ApiKeyBravo
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

            services.AddDbContext<ApiTesterDbContext>(opt => opt.UseSqlite(_connection));
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
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }

    public const string ApiKeyAlpha = "dev-local-key";
    public const string ApiKeyBravo = "dev-secondary-key";
}
