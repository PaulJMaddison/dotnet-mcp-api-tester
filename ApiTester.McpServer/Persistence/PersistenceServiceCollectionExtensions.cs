using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
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
        services.AddSingleton<RedactionService>();

        services.AddSingleton<FileTestRunStore>();
        services.AddSingleton<FileProjectStore>();
        services.AddSingleton<FileOpenApiSpecStore>();
        services.AddSingleton<FileTestPlanStore>();
        services.AddSingleton<FileTestPlanDraftStore>();
        services.AddSingleton<FileEnvironmentStore>();
        services.AddSingleton<FileRunAnnotationStore>();
        services.AddSingleton<FileOrganisationStore>();
        services.AddSingleton<FileUserStore>();
        services.AddSingleton<FileMembershipStore>();
        services.AddSingleton<FileApiKeyStore>();
        services.AddSingleton<FileAuditEventStore>();
        services.AddSingleton<FileBaselineStore>();
        services.AddSingleton<FileAiInsightStore>();
        services.AddSingleton<FileGeneratedDocsStore>();
        services.AddSingleton<FileSubscriptionStore>();
        services.AddScoped<SqlTestRunStore>();
        services.AddScoped<SqlProjectStore>();
        services.AddScoped<SqlOrganisationStore>();
        services.AddScoped<SqlUserStore>();
        services.AddScoped<SqlMembershipStore>();
        services.AddScoped<SqlApiKeyStore>();
        services.AddScoped<SqlAuditEventStore>();
        services.AddScoped<SqlBaselineStore>();
        services.AddScoped<SqlAiInsightStore>();
        services.AddScoped<SqlGeneratedDocsStore>();
        services.AddScoped<SqlSubscriptionStore>();

        if (selection.UseSqlProvider)
        {
            services.AddDbContext<ApiTesterDbContext>(opt =>
            {
                if (selection.Provider == PersistenceProvider.SqlServer)
                    opt.UseSqlServer(selection.ConnectionString);
                else if (selection.Provider == PersistenceProvider.PostgreSql)
                    opt.UseNpgsql(selection.ConnectionString);
            });

            services.AddScoped<SqlOpenApiSpecStore>();
            services.AddScoped<SqlTestPlanStore>();
            services.AddScoped<SqlTestPlanDraftStore>();
            services.AddScoped<SqlEnvironmentStore>();
            services.AddScoped<SqlRunAnnotationStore>();
            services.AddScoped<SqlOrganisationStore>();
            services.AddScoped<SqlUserStore>();
            services.AddScoped<SqlMembershipStore>();
            services.AddScoped<SqlApiKeyStore>();
            services.AddScoped<SqlAuditEventStore>();
            services.AddScoped<SqlBaselineStore>();
            services.AddScoped<SqlAiInsightStore>();
            services.AddScoped<SqlGeneratedDocsStore>();
            services.AddScoped<SqlSubscriptionStore>();
            services.AddScoped<IOpenApiSpecStore>(sp => sp.GetRequiredService<SqlOpenApiSpecStore>());
            services.AddScoped<ITestPlanStore>(sp => sp.GetRequiredService<SqlTestPlanStore>());
            services.AddScoped<ITestPlanDraftStore>(sp => sp.GetRequiredService<SqlTestPlanDraftStore>());
            services.AddScoped<IEnvironmentStore>(sp => sp.GetRequiredService<SqlEnvironmentStore>());
            services.AddScoped<IRunAnnotationStore>(sp => sp.GetRequiredService<SqlRunAnnotationStore>());
            services.AddScoped<IOrganisationStore>(sp => sp.GetRequiredService<SqlOrganisationStore>());
            services.AddScoped<IUserStore>(sp => sp.GetRequiredService<SqlUserStore>());
            services.AddScoped<IMembershipStore>(sp => sp.GetRequiredService<SqlMembershipStore>());
            services.AddScoped<IApiKeyStore>(sp => sp.GetRequiredService<SqlApiKeyStore>());
            services.AddScoped<IAuditEventStore>(sp => sp.GetRequiredService<SqlAuditEventStore>());
            services.AddScoped<IBaselineStore>(sp => sp.GetRequiredService<SqlBaselineStore>());
            services.AddScoped<IAiInsightStore>(sp => sp.GetRequiredService<SqlAiInsightStore>());
            services.AddScoped<IGeneratedDocsStore>(sp => sp.GetRequiredService<SqlGeneratedDocsStore>());
            services.AddScoped<ISubscriptionStore>(sp => sp.GetRequiredService<SqlSubscriptionStore>());
        }
        else
        {
            services.AddSingleton<IOpenApiSpecStore>(sp => sp.GetRequiredService<FileOpenApiSpecStore>());
            services.AddSingleton<ITestPlanStore>(sp => sp.GetRequiredService<FileTestPlanStore>());
            services.AddSingleton<ITestPlanDraftStore>(sp => sp.GetRequiredService<FileTestPlanDraftStore>());
            services.AddSingleton<IEnvironmentStore>(sp => sp.GetRequiredService<FileEnvironmentStore>());
            services.AddSingleton<IRunAnnotationStore>(sp => sp.GetRequiredService<FileRunAnnotationStore>());
            services.AddSingleton<IOrganisationStore>(sp => sp.GetRequiredService<FileOrganisationStore>());
            services.AddSingleton<IUserStore>(sp => sp.GetRequiredService<FileUserStore>());
            services.AddSingleton<IMembershipStore>(sp => sp.GetRequiredService<FileMembershipStore>());
            services.AddSingleton<IApiKeyStore>(sp => sp.GetRequiredService<FileApiKeyStore>());
            services.AddSingleton<IAuditEventStore>(sp => sp.GetRequiredService<FileAuditEventStore>());
            services.AddSingleton<IBaselineStore>(sp => sp.GetRequiredService<FileBaselineStore>());
            services.AddSingleton<IAiInsightStore>(sp => sp.GetRequiredService<FileAiInsightStore>());
            services.AddSingleton<IGeneratedDocsStore>(sp => sp.GetRequiredService<FileGeneratedDocsStore>());
            services.AddSingleton<ISubscriptionStore>(sp => sp.GetRequiredService<FileSubscriptionStore>());
        }

        services.AddScoped<ITestRunStore>(sp => UseSqlPersistence(sp)
            ? sp.GetRequiredService<SqlTestRunStore>()
            : sp.GetRequiredService<FileTestRunStore>());
        services.AddScoped<IProjectStore>(sp => UseSqlPersistence(sp)
            ? sp.GetRequiredService<SqlProjectStore>()
            : sp.GetRequiredService<FileProjectStore>());

        return services;
    }

    private static bool UseSqlPersistence(IServiceProvider services)
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PersistenceOptions>>().Value;
        return PersistenceProviderSelector.Select(options).UseSqlProvider;
    }
}
