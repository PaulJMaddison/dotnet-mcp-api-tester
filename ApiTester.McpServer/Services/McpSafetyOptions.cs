namespace ApiTester.McpServer.Services;

/// <summary>
/// Process-start safety switches for MCP capabilities that can loosen execution controls.
/// These are intentionally not mutable through MCP tools.
/// </summary>
public sealed record McpSafetyOptions(bool AllowPolicyMutation)
{
    public static McpSafetyOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration.GetValue<bool?>("McpSafety:AllowPolicyMutation");
        if (configured.HasValue)
            return new McpSafetyOptions(configured.Value);

        var raw = Environment.GetEnvironmentVariable("APITESTER_MCP_ALLOW_POLICY_MUTATION");
        return new McpSafetyOptions(bool.TryParse(raw, out var enabled) && enabled);
    }
}
