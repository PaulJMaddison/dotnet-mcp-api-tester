using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Auth;
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
            services.RemoveAll<ITestPlanDraftStore>();
            services.RemoveAll<IOrganisationStore>();
            services.RemoveAll<IUserStore>();
            services.RemoveAll<IMembershipStore>();
            services.RemoveAll<IApiKeyStore>();
            services.RemoveAll<IAuditEventStore>();
            services.RemoveAll<IGeneratedDocsStore>();

            services.AddDbContext<ApiTesterDbContext>(opt => opt.UseSqlite(_connection));
            services.AddScoped<SqlTestRunStore>();
            services.AddScoped<SqlProjectStore>();
            services.AddScoped<SqlOpenApiSpecStore>();
            services.AddScoped<SqlTestPlanStore>();
            services.AddScoped<SqlTestPlanDraftStore>();
            services.AddScoped<SqlOrganisationStore>();
            services.AddScoped<SqlUserStore>();
            services.AddScoped<SqlMembershipStore>();
            services.AddScoped<SqlApiKeyStore>();
            services.AddScoped<SqlAuditEventStore>();
            services.AddScoped<SqlGeneratedDocsStore>();
            services.AddScoped<ITestRunStore>(sp => sp.GetRequiredService<SqlTestRunStore>());
            services.AddScoped<IProjectStore>(sp => sp.GetRequiredService<SqlProjectStore>());
            services.AddScoped<IOpenApiSpecStore>(sp => sp.GetRequiredService<SqlOpenApiSpecStore>());
            services.AddScoped<ITestPlanStore>(sp => sp.GetRequiredService<SqlTestPlanStore>());
            services.AddScoped<ITestPlanDraftStore>(sp => sp.GetRequiredService<SqlTestPlanDraftStore>());
            services.AddScoped<IOrganisationStore>(sp => sp.GetRequiredService<SqlOrganisationStore>());
            services.AddScoped<IUserStore>(sp => sp.GetRequiredService<SqlUserStore>());
            services.AddScoped<IMembershipStore>(sp => sp.GetRequiredService<SqlMembershipStore>());
            services.AddScoped<IApiKeyStore>(sp => sp.GetRequiredService<SqlApiKeyStore>());
            services.AddScoped<IAuditEventStore>(sp => sp.GetRequiredService<SqlAuditEventStore>());
            services.AddScoped<IGeneratedDocsStore>(sp => sp.GetRequiredService<SqlGeneratedDocsStore>());

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
            db.Database.EnsureCreated();
            SeedIdentity(db);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }

    public static readonly Guid OrganisationAlphaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid OrganisationBravoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid UserAlphaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserBravoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public const string AlphaExternalId = "alpha-user";
    public const string BravoExternalId = "bravo-user";
    public const string ApiKeyAlpha = "alpha01.aabbccddeeff00112233445566778899";
    public const string ApiKeyBravo = "bravo01.ffeeddccbbaa99887766554433221100";

    private static void SeedIdentity(ApiTesterDbContext db)
    {
        if (!ApiKeyToken.TryGetPrefix(ApiKeyAlpha, out var alphaPrefix))
            throw new InvalidOperationException("Invalid ApiKeyAlpha format.");
        if (!ApiKeyToken.TryGetPrefix(ApiKeyBravo, out var bravoPrefix))
            throw new InvalidOperationException("Invalid ApiKeyBravo format.");

        var now = DateTime.UtcNow;

        if (!db.Organisations.Any(o => o.OrganisationId == OrganisationAlphaId))
        {
            db.Organisations.Add(new OrganisationEntity
            {
                OrganisationId = OrganisationAlphaId,
                Name = "Alpha Org",
                Slug = "alpha-org",
                CreatedUtc = now
            });
        }

        if (!db.Organisations.Any(o => o.OrganisationId == OrganisationBravoId))
        {
            db.Organisations.Add(new OrganisationEntity
            {
                OrganisationId = OrganisationBravoId,
                Name = "Bravo Org",
                Slug = "bravo-org",
                CreatedUtc = now
            });
        }

        if (!db.Users.Any(u => u.UserId == UserAlphaId))
        {
            db.Users.Add(new UserEntity
            {
                UserId = UserAlphaId,
                ExternalId = AlphaExternalId,
                DisplayName = "Alpha User",
                Email = "alpha@example.test",
                CreatedUtc = now
            });
        }

        if (!db.Users.Any(u => u.UserId == UserBravoId))
        {
            db.Users.Add(new UserEntity
            {
                UserId = UserBravoId,
                ExternalId = BravoExternalId,
                DisplayName = "Bravo User",
                Email = "bravo@example.test",
                CreatedUtc = now
            });
        }

        if (!db.Memberships.Any(m => m.OrganisationId == OrganisationAlphaId && m.UserId == UserAlphaId))
        {
            db.Memberships.Add(new MembershipEntity
            {
                OrganisationId = OrganisationAlphaId,
                UserId = UserAlphaId,
                Role = OrgRole.Owner,
                CreatedUtc = now
            });
        }

        if (!db.Memberships.Any(m => m.OrganisationId == OrganisationBravoId && m.UserId == UserBravoId))
        {
            db.Memberships.Add(new MembershipEntity
            {
                OrganisationId = OrganisationBravoId,
                UserId = UserBravoId,
                Role = OrgRole.Member,
                CreatedUtc = now
            });
        }

        if (!db.ApiKeys.Any(k => k.Prefix == alphaPrefix))
        {
            db.ApiKeys.Add(new ApiKeyEntity
            {
                KeyId = Guid.NewGuid(),
                OrganisationId = OrganisationAlphaId,
                UserId = UserAlphaId,
                Name = "Alpha Admin",
                Scopes = ApiKeyScopes.Serialize(new[]
                {
                    ApiKeyScopes.ProjectsRead,
                    ApiKeyScopes.ProjectsWrite,
                    ApiKeyScopes.RunsRead,
                    ApiKeyScopes.RunsWrite,
                    ApiKeyScopes.AdminKeys
                }),
                ExpiresUtc = null,
                RevokedUtc = null,
                Hash = ApiKeyHasher.Hash(ApiKeyAlpha),
                Prefix = alphaPrefix
            });
        }

        if (!db.ApiKeys.Any(k => k.Prefix == bravoPrefix))
        {
            db.ApiKeys.Add(new ApiKeyEntity
            {
                KeyId = Guid.NewGuid(),
                OrganisationId = OrganisationBravoId,
                UserId = UserBravoId,
                Name = "Bravo Reader",
                Scopes = ApiKeyScopes.Serialize(new[] { ApiKeyScopes.ProjectsRead }),
                ExpiresUtc = null,
                RevokedUtc = null,
                Hash = ApiKeyHasher.Hash(ApiKeyBravo),
                Prefix = bravoPrefix
            });
        }

        db.SaveChanges();
    }
}
