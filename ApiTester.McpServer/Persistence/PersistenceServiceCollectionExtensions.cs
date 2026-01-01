using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.McpServer.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddApiTesterPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Persistence").Get<PersistenceOptions>() ?? new PersistenceOptions();
        var selection = PersistenceProviderSelector.Select(options);

        services.Configure<PersistenceOptions>(configuration.GetSection("Persistence"));

        services.AddSingleton<FileTestRunStore>();
        services.AddSingleton<FileProjectStore>();
        services.AddSingleton<FileOpenApiSpecStore>();
        services.AddSingleton<FileTestPlanStore>();
        services.AddSingleton<FileEnvironmentStore>();
        services.AddSingleton<FileRunAnnotationStore>();

        if (selection.UseSqlProvider)
        {
            services.AddDbContext<ApiTesterDbContext>(opt =>
            {
                if (selection.Provider == PersistenceProvider.SqlServer)
                    opt.UseSqlServer(selection.ConnectionString);
                else if (selection.Provider == PersistenceProvider.Sqlite)
                    opt.UseSqlite(selection.ConnectionString);
            });

            services.AddScoped<SqlTestRunStore>();
            services.AddScoped<SqlProjectStore>();
            services.AddScoped<SqlOpenApiSpecStore>();
            services.AddScoped<SqlTestPlanStore>();
            services.AddScoped<SqlEnvironmentStore>();
            services.AddScoped<SqlRunAnnotationStore>();
            services.AddScoped<ITestRunStore>(sp => sp.GetRequiredService<SqlTestRunStore>());
            services.AddScoped<IProjectStore>(sp => sp.GetRequiredService<SqlProjectStore>());
            services.AddScoped<IOpenApiSpecStore>(sp => sp.GetRequiredService<SqlOpenApiSpecStore>());
            services.AddScoped<ITestPlanStore>(sp => sp.GetRequiredService<SqlTestPlanStore>());
            services.AddScoped<IEnvironmentStore>(sp => sp.GetRequiredService<SqlEnvironmentStore>());
            services.AddScoped<IRunAnnotationStore>(sp => sp.GetRequiredService<SqlRunAnnotationStore>());
        }
        else
        {
            services.AddSingleton<ITestRunStore>(sp => sp.GetRequiredService<FileTestRunStore>());
            services.AddSingleton<IProjectStore>(sp => sp.GetRequiredService<FileProjectStore>());
            services.AddSingleton<IOpenApiSpecStore>(sp => sp.GetRequiredService<FileOpenApiSpecStore>());
            services.AddSingleton<ITestPlanStore>(sp => sp.GetRequiredService<FileTestPlanStore>());
            services.AddSingleton<IEnvironmentStore>(sp => sp.GetRequiredService<FileEnvironmentStore>());
            services.AddSingleton<IRunAnnotationStore>(sp => sp.GetRequiredService<FileRunAnnotationStore>());
        }

        return services;
    }
}
