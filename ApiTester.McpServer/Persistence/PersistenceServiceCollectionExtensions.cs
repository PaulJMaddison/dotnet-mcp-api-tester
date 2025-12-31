using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiTester.McpServer.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddApiTesterPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(configuration.GetSection("Persistence"));

        services.AddSingleton<FileTestRunStore>();
        services.AddSingleton<FileProjectStore>();

        services.AddDbContext<ApiTesterDbContext>((sp, opt) =>
        {
            var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var provider = (p.Provider ?? "File").Trim();
            var cs = (p.ConnectionString ?? "").Trim();

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs))
                opt.UseSqlServer(cs);
        });

        services.AddScoped<SqlTestRunStore>();
        services.AddScoped<SqlProjectStore>();

        services.AddScoped<ITestRunStore>(sp =>
        {
            var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var provider = (p.Provider ?? "File").Trim();
            var cs = (p.ConnectionString ?? "").Trim();

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs))
                return sp.GetRequiredService<SqlTestRunStore>();

            return sp.GetRequiredService<FileTestRunStore>();
        });

        services.AddScoped<IProjectStore>(sp =>
        {
            var p = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var provider = (p.Provider ?? "File").Trim();
            var cs = (p.ConnectionString ?? "").Trim();

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cs))
                return sp.GetRequiredService<SqlProjectStore>();

            return sp.GetRequiredService<FileProjectStore>();
        });

        return services;
    }
}
