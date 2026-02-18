using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApiTester.Cli;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.IntegrationTests;

public sealed class CliSmokeEndpointsTests(ApiTesterWebFactory factory) : IClassFixture<ApiTesterWebFactory>
{
    [Fact]
    public async Task ProjectsList_Command_WritesProjectRows_WithBearerToken()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Api-Key", ApiTesterWebFactory.ApiKeyAlpha);

        var createResponse = await admin.PostAsJsonAsync("/api/v1/tokens", new ApiKeyCreateRequest("cli-test", new List<string> { "projects:read" }, null));
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreateResponse>();
        Assert.NotNull(created);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created!.Token);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var options = new CliOptions(client.BaseAddress!, created.Token, new CliCommand.ProjectsList());

        var code = await CliApp.ExecuteCommandAsync(client, options, stdout, stderr, CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Empty(stderr.ToString());
        Assert.NotNull(stdout.ToString());
    }
}
