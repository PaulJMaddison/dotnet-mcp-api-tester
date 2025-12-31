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
        services.AddSingleton(new AppConfig { WorkingDirectory = Path.GetTempPath() });
        services.AddApiTesterPersistence(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();

        Assert.IsType<FileProjectStore>(store);
        Assert.Null(scope.ServiceProvider.GetService<ApiTesterDbContext>());
    }

    [Fact]
    public void AddApiTesterPersistence_UsesSqlStores_WhenProviderIsSqlite()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Sqlite",
                ["Persistence:ConnectionString"] = "Data Source=:memory:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig { WorkingDirectory = Path.GetTempPath() });
        services.AddApiTesterPersistence(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IProjectStore>();

        Assert.IsType<SqlProjectStore>(store);
        Assert.NotNull(scope.ServiceProvider.GetService<ApiTesterDbContext>());
    }
}
