using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiTester.Web.IntegrationTests;

public sealed class ApiTesterWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public ApiTesterWebFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApiTesterDbContext>>();
            services.AddDbContext<ApiTesterDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<ITestRunStore>();
            services.RemoveAll<IProjectStore>();
            services.AddScoped<ITestRunStore, SqlTestRunStore>();
            services.AddScoped<IProjectStore, SqlProjectStore>();

            services.Configure<PersistenceOptions>(options =>
            {
                options.Provider = "Sqlite";
                options.ConnectionString = _connection.ConnectionString;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
