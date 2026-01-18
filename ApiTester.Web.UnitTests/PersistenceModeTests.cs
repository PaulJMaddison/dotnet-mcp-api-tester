using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class PersistenceModeTests
{
    [Fact]
    public async Task FileMode_CanCreateProjectAndRun()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"apitester-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "File"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(new AppConfig { WorkingDirectory = tempDir });
            services.AddApiTesterPersistence(config);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectStore>();
            var runs = scope.ServiceProvider.GetRequiredService<ITestRunStore>();

            var project = await projects.CreateAsync(OrgDefaults.DefaultOrganisationId, "owner", "File Project", CancellationToken.None);
            var runRecord = new TestRunRecord
            {
                RunId = Guid.NewGuid(),
                OrganisationId = project.OrganisationId,
                OwnerKey = project.OwnerKey,
                ProjectKey = project.ProjectKey,
                OperationId = "op",
                StartedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = DateTimeOffset.UtcNow,
                Result = new TestRunResult
                {
                    OperationId = "op",
                    TotalCases = 0,
                    Passed = 0,
                    Failed = 0,
                    Blocked = 0,
                    TotalDurationMs = 0
                }
            };

            await runs.SaveAsync(runRecord);
            var loaded = await runs.GetAsync(project.OrganisationId, runRecord.RunId);

            Assert.NotNull(loaded);
            Assert.Equal(project.ProjectKey, loaded!.ProjectKey);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ToolsActivation_WiresProjectTools()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "File"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new AppConfig { WorkingDirectory = Path.GetTempPath() });
        services.AddApiTesterPersistence(config);
        services.AddScoped<ProjectContext>();
        services.AddTransient<ProjectTools>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<ProjectTools>();

        Assert.NotNull(tools);
    }

    [Fact]
    public async Task SqlServerMode_CanCreateProjectAndRun_WhenConnectionStringConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable("APITESTER_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "SqlServer",
                ["Persistence:ConnectionString"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new AppConfig { WorkingDirectory = Path.GetTempPath() });
        services.AddApiTesterPersistence(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var projects = scope.ServiceProvider.GetRequiredService<IProjectStore>();
        var runs = scope.ServiceProvider.GetRequiredService<ITestRunStore>();

        var project = await projects.CreateAsync(OrgDefaults.DefaultOrganisationId, "owner", "Sql Project", CancellationToken.None);
        var runRecord = new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            OrganisationId = project.OrganisationId,
            OwnerKey = project.OwnerKey,
            ProjectKey = project.ProjectKey,
            OperationId = "op",
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = "op",
                TotalCases = 0,
                Passed = 0,
                Failed = 0,
                Blocked = 0,
                TotalDurationMs = 0
            }
        };

        await runs.SaveAsync(runRecord);
        var loaded = await runs.GetAsync(project.OrganisationId, runRecord.RunId);

        Assert.NotNull(loaded);
        Assert.Equal(project.ProjectKey, loaded!.ProjectKey);
    }
}
