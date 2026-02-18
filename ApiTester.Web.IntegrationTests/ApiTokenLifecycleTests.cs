using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.Web.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTester.Web.IntegrationTests;

public sealed class ApiTokenLifecycleTests
{
    [Fact]
    public async Task CreateToken_AllowsBearerAccess_ThenRevokeReturnsUnauthorized()
    {
        using var factory = new ApiTesterWebFactory();
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Api-Key", ApiTesterWebFactory.ApiKeyAlpha);

        var createResponse = await admin.PostAsJsonAsync("/api/v1/tokens", new ApiKeyCreateRequest("ci-token", new List<string> { "projects:read" }, null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreateResponse>();
        Assert.NotNull(created);

        var bearer = factory.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created!.Token);
        var protectedResponse = await bearer.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);

        var revokeResponse = await admin.PostAsync($"/api/v1/tokens/{created.ApiKey.KeyId}/revoke", null);
        revokeResponse.EnsureSuccessStatusCode();

        var afterRevoke = await bearer.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task Token_IsTenantScoped_CrossTenantProjectAccessBlocked()
    {
        using var factory = new ApiTesterWebFactory();
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Api-Key", ApiTesterWebFactory.ApiKeyAlpha);

        Guid bravoProjectId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
            var project = new ProjectEntity
            {
                ProjectId = Guid.NewGuid(),
                OrganisationId = ApiTesterWebFactory.OrganisationBravoId,
                TenantId = ApiTesterWebFactory.OrganisationBravoId,
                Name = "Bravo Private",
                OwnerKey = "bravo-owner",
                ProjectKey = "bravo-private",
                CreatedUtc = DateTime.UtcNow
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            bravoProjectId = project.ProjectId;
        }

        var createResponse = await admin.PostAsJsonAsync("/api/v1/tokens", new ApiKeyCreateRequest("tenant-token", new List<string> { "projects:read" }, null));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreateResponse>();

        var bearer = factory.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created!.Token);
        var crossTenant = await bearer.GetAsync($"/api/projects/{bravoProjectId}");

        Assert.Equal(HttpStatusCode.Forbidden, crossTenant.StatusCode);
    }
}
