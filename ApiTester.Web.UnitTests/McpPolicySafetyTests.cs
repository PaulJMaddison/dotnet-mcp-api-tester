using System.Text.Json;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class McpPolicySafetyTests
{
    [Fact]
    public async Task ApiSetPolicy_IsDeniedWhenProcessGateIsDisabled()
    {
        var runtime = new ApiRuntimeConfig();
        var tools = new PolicyTools(runtime, new NullAuditEventStore(), new McpSafetyOptions(false));

        var response = await tools.ApiSetPolicy("""
            {
              "dryRun": false,
              "blockLocalhost": false,
              "allowedMethods": ["GET", "POST"],
              "allowedBaseUrls": ["http://127.0.0.1:5055"]
            }
            """);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.True(json.RootElement.GetProperty("isError").GetBoolean());
        Assert.Contains("disabled", json.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(runtime.Policy.DryRun);
        Assert.Empty(runtime.Policy.AllowedBaseUrls);
        Assert.True(runtime.Policy.BlockLocalhost);
    }

    [Fact]
    public async Task ApiSetPolicy_CanBeEnabledOnlyByConstructionTimeSafetyOption()
    {
        var runtime = new ApiRuntimeConfig();
        var tools = new PolicyTools(runtime, new NullAuditEventStore(), new McpSafetyOptions(true));

        var response = await tools.ApiSetPolicy("""
            {
              "dryRun": false,
              "blockLocalhost": false,
              "allowedMethods": ["GET"],
              "allowedBaseUrls": ["http://127.0.0.1:5055"]
            }
            """);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(runtime.Policy.DryRun);
        Assert.False(runtime.Policy.BlockLocalhost);
        Assert.Contains("http://127.0.0.1:5055", runtime.Policy.AllowedBaseUrls);
    }

    [Fact]
    public void ApiGetPolicy_ExposesMutationGateWithoutSecrets()
    {
        var tools = new PolicyTools(new ApiRuntimeConfig(), new NullAuditEventStore(), new McpSafetyOptions(false));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(tools.ApiGetPolicy()));
        Assert.False(json.RootElement.GetProperty("mcpPolicyMutationEnabled").GetBoolean());
    }
}
