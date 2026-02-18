namespace ApiTester.Site.Tests;

public class AppAuthenticationIntegrationTests : IClassFixture<SiteWebApplicationFactory>
{
    private readonly SiteWebApplicationFactory _factory;

    public AppAuthenticationIntegrationTests(SiteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthenticated_AppProjects_RedirectsToSignIn()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/app/projects");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/signin", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Authenticated_User_SignIn_RedirectsToApp()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "authenticated");

        using var redirectClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        redirectClient.DefaultRequestHeaders.Add("X-Test-Auth", "authenticated");

        var response = await redirectClient.GetAsync("/signin");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/app", response.Headers.Location?.OriginalString);
    }
}
