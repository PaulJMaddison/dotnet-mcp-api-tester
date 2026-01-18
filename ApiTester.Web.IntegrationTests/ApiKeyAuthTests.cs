using System.Net;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.Web.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApiTester.Web.IntegrationTests;

public sealed class ApiKeyAuthTests
{
    [Fact]
    public async Task GetProjects_ReturnsForbidden_WhenScopeMissing()
    {
        using var factory = new ApiTesterWebFactory();
        var apiKey = await SeedApiKeyAsync(factory, new[] { ApiKeyScopes.RunsRead }, null, null);

        var client = CreateClient(factory, apiKey);
        var response = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProjects_ReturnsForbidden_WhenExpired()
    {
        using var factory = new ApiTesterWebFactory();
        var expired = DateTime.UtcNow.AddMinutes(-5);
        var apiKey = await SeedApiKeyAsync(factory, new[] { ApiKeyScopes.ProjectsRead }, expired, null);

        var client = CreateClient(factory, apiKey);
        var response = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProjects_ReturnsForbidden_WhenRevoked()
    {
        using var factory = new ApiTesterWebFactory();
        var revoked = DateTime.UtcNow.AddMinutes(-1);
        var apiKey = await SeedApiKeyAsync(factory, new[] { ApiKeyScopes.ProjectsRead }, null, revoked);

        var client = CreateClient(factory, apiKey);
        var response = await client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> SeedApiKeyAsync(WebApplicationFactory<Program> factory, IEnumerable<string> scopes, DateTime? expiresUtc, DateTime? revokedUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiTesterDbContext>();
        await db.Database.EnsureCreatedAsync();

        var token = ApiKeyToken.Generate();
        db.ApiKeys.Add(new ApiKeyEntity
        {
            KeyId = Guid.NewGuid(),
            OrganisationId = ApiTesterWebFactory.OrganisationAlphaId,
            UserId = ApiTesterWebFactory.UserAlphaId,
            Name = "Test Key",
            Scopes = ApiKeyScopes.Serialize(scopes),
            ExpiresUtc = expiresUtc,
            RevokedUtc = revokedUtc,
            Hash = ApiKeyHasher.Hash(token.Token),
            Prefix = token.Prefix
        });

        await db.SaveChangesAsync();
        return token.Token;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthDefaults.HeaderName, apiKey);
        return client;
    }
}
