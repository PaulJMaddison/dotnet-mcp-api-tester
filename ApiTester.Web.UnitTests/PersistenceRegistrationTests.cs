using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class PersistenceRegistrationTests
{
    [Fact]
    public void AddApiTesterPersistence_UsesFileStores_WhenProviderIsFile()
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

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
        var runStore = scope.ServiceProvider.GetRequiredService<ITestRunStore>();
        var openApiStore = scope.ServiceProvider.GetRequiredService<IOpenApiSpecStore>();
        var environmentStore = scope.ServiceProvider.GetRequiredService<IEnvironmentStore>();

        Assert.IsType<FileProjectStore>(store);
        Assert.IsType<FileTestRunStore>(runStore);
        Assert.IsType<FileOpenApiSpecStore>(openApiStore);
        Assert.IsType<FileEnvironmentStore>(environmentStore);
        Assert.Null(scope.ServiceProvider.GetService<ApiTesterDbContext>());
    }

    [Fact]
    public void AddApiTesterPersistence_UsesSqlStores_WhenProviderIsSqlServer()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "SqlServer",
                ["Persistence:ConnectionString"] = "Server=localhost;Database=ApiTester;User Id=sa;Password=Pass@word1;TrustServerCertificate=True;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new AppConfig { WorkingDirectory = Path.GetTempPath() });
        services.AddApiTesterPersistence(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();
        var runStore = scope.ServiceProvider.GetRequiredService<ITestRunStore>();
        var openApiStore = scope.ServiceProvider.GetRequiredService<IOpenApiSpecStore>();
        var environmentStore = scope.ServiceProvider.GetRequiredService<IEnvironmentStore>();

        Assert.IsType<SqlProjectStore>(store);
        Assert.IsType<SqlTestRunStore>(runStore);
        Assert.IsType<SqlOpenApiSpecStore>(openApiStore);
        Assert.IsType<SqlEnvironmentStore>(environmentStore);
        Assert.NotNull(scope.ServiceProvider.GetService<ApiTesterDbContext>());
    }
}
