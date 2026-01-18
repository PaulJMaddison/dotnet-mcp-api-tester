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
        services.AddSingleton<FileOrganisationStore>();
        services.AddSingleton<FileUserStore>();
        services.AddSingleton<FileMembershipStore>();
        services.AddScoped<SqlTestRunStore>();
        services.AddScoped<SqlProjectStore>();
        services.AddScoped<SqlOrganisationStore>();
        services.AddScoped<SqlUserStore>();
        services.AddScoped<SqlMembershipStore>();

        if (selection.UseSqlProvider)
        {
            services.AddDbContext<ApiTesterDbContext>(opt =>
            {
                if (selection.Provider == PersistenceProvider.SqlServer)
                    opt.UseSqlServer(selection.ConnectionString);
            });

            services.AddScoped<SqlOpenApiSpecStore>();
            services.AddScoped<SqlTestPlanStore>();
            services.AddScoped<SqlEnvironmentStore>();
            services.AddScoped<SqlRunAnnotationStore>();
            services.AddScoped<SqlOrganisationStore>();
            services.AddScoped<SqlUserStore>();
            services.AddScoped<SqlMembershipStore>();
            services.AddScoped<IOpenApiSpecStore>(sp => sp.GetRequiredService<SqlOpenApiSpecStore>());
            services.AddScoped<ITestPlanStore>(sp => sp.GetRequiredService<SqlTestPlanStore>());
            services.AddScoped<IEnvironmentStore>(sp => sp.GetRequiredService<SqlEnvironmentStore>());
            services.AddScoped<IRunAnnotationStore>(sp => sp.GetRequiredService<SqlRunAnnotationStore>());
            services.AddScoped<IOrganisationStore>(sp => sp.GetRequiredService<SqlOrganisationStore>());
            services.AddScoped<IUserStore>(sp => sp.GetRequiredService<SqlUserStore>());
            services.AddScoped<IMembershipStore>(sp => sp.GetRequiredService<SqlMembershipStore>());
        }
        else
        {
            services.AddSingleton<IOpenApiSpecStore>(sp => sp.GetRequiredService<FileOpenApiSpecStore>());
            services.AddSingleton<ITestPlanStore>(sp => sp.GetRequiredService<FileTestPlanStore>());
            services.AddSingleton<IEnvironmentStore>(sp => sp.GetRequiredService<FileEnvironmentStore>());
            services.AddSingleton<IRunAnnotationStore>(sp => sp.GetRequiredService<FileRunAnnotationStore>());
            services.AddSingleton<IOrganisationStore>(sp => sp.GetRequiredService<FileOrganisationStore>());
            services.AddSingleton<IUserStore>(sp => sp.GetRequiredService<FileUserStore>());
            services.AddSingleton<IMembershipStore>(sp => sp.GetRequiredService<FileMembershipStore>());
        }

        services.AddScoped<ITestRunStore>(sp => UseSqlServer(sp)
            ? sp.GetRequiredService<SqlTestRunStore>()
            : sp.GetRequiredService<FileTestRunStore>());
        services.AddScoped<IProjectStore>(sp => UseSqlServer(sp)
            ? sp.GetRequiredService<SqlProjectStore>()
            : sp.GetRequiredService<FileProjectStore>());

        return services;
    }

    private static bool UseSqlServer(IServiceProvider services)
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PersistenceOptions>>().Value;
        return PersistenceProviderSelector.Select(options).UseSqlProvider;
    }
}
