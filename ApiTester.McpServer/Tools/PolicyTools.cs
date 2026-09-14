using System.ComponentModel;
using System.Text.Json;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class PolicyTools
{
    private readonly ApiRuntimeConfig _runtime;
    private readonly McpSafetyOptions _safety;

    public PolicyTools(ApiRuntimeConfig runtime, McpSafetyOptions safety)
    {
        _runtime = runtime;
        _safety = safety;
    }

    [McpServerTool, Description("Get the current in-memory execution policy.")]
    public object ApiGetPolicy() => new
    {
        dryRun = _runtime.Policy.DryRun,
        allowedBaseUrls = _runtime.Policy.AllowedBaseUrls,
        allowedMethods = _runtime.Policy.AllowedMethods.ToArray(),
        timeoutSeconds = (int)_runtime.Policy.Timeout.TotalSeconds,
        maxRequestBodyBytes = _runtime.Policy.MaxRequestBodyBytes,
        maxResponseBodyBytes = _runtime.Policy.MaxResponseBodyBytes,
        blockLocalhost = _runtime.Policy.BlockLocalhost,
        blockPrivateNetworks = _runtime.Policy.BlockPrivateNetworks,
        mcpPolicyMutationEnabled = _safety.AllowPolicyMutation,
        persistence = "none"
    };

    [McpServerTool, Description("Reset execution policy to safe dry-run, deny-by-default settings.")]
    public object ApiResetPolicy()
    {
        ApiPolicyDefaults.ApplySafeDefaults(_runtime.Policy);
        return new { ok = true, policy = ApiGetPolicy() };
    }

    [McpServerTool, Description("Update the in-memory execution policy. Disabled unless the MCP process was started with policy mutation explicitly enabled.")]
    public object ApiSetPolicy(string policyJson)
    {
        if (!_safety.AllowPolicyMutation)
        {
            return new
            {
                isError = true,
                error = "Policy mutation is disabled. Set APITESTER_MCP_ALLOW_POLICY_MUTATION=true before starting the server only when you intentionally want the agent to change execution policy."
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(policyJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("policyJson must be a JSON object.");

            var next = Clone(_runtime.Policy);
            if (root.TryGetProperty("dryRun", out var dryRun)) next.DryRun = dryRun.GetBoolean();
            if (root.TryGetProperty("blockLocalhost", out var blockLocalhost)) next.BlockLocalhost = blockLocalhost.GetBoolean();
            if (root.TryGetProperty("blockPrivateNetworks", out var blockPrivate)) next.BlockPrivateNetworks = blockPrivate.GetBoolean();

            if (root.TryGetProperty("timeoutSeconds", out var timeout))
                next.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeout.GetInt32(), 1, 60));

            if (root.TryGetProperty("maxRequestBodyBytes", out var maxRequest))
                next.MaxRequestBodyBytes = RequireNonNegative(maxRequest.GetInt32(), "maxRequestBodyBytes");

            if (root.TryGetProperty("maxResponseBodyBytes", out var maxResponse))
                next.MaxResponseBodyBytes = RequireNonNegative(maxResponse.GetInt32(), "maxResponseBodyBytes");

            if (root.TryGetProperty("allowedMethods", out var methods) && methods.ValueKind == JsonValueKind.Array)
            {
                next.AllowedMethods.Clear();
                foreach (var item in methods.EnumerateArray())
                {
                    var method = (item.GetString() ?? string.Empty).Trim().ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(method)) next.AllowedMethods.Add(method);
                }
                if (next.AllowedMethods.Count == 0) next.AllowedMethods.Add("GET");
            }

            if (root.TryGetProperty("allowedBaseUrls", out var urls) && urls.ValueKind == JsonValueKind.Array)
            {
                next.AllowedBaseUrls.Clear();
                foreach (var item in urls.EnumerateArray())
                {
                    var value = (item.GetString() ?? string.Empty).Trim().TrimEnd('/');
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                        throw new InvalidOperationException($"Invalid allowedBaseUrl: {value}");
                    next.AllowedBaseUrls.Add(value);
                }
            }

            Apply(next);
            return new { ok = true, policy = ApiGetPolicy() };
        }
        catch (Exception ex)
        {
            return new { isError = true, error = ex.Message };
        }
    }

    private void Apply(ApiExecutionPolicy policy)
    {
        _runtime.Policy.DryRun = policy.DryRun;
        _runtime.Policy.BlockLocalhost = policy.BlockLocalhost;
        _runtime.Policy.BlockPrivateNetworks = policy.BlockPrivateNetworks;
        _runtime.Policy.Timeout = policy.Timeout;
        _runtime.Policy.MaxRequestBodyBytes = policy.MaxRequestBodyBytes;
        _runtime.Policy.MaxResponseBodyBytes = policy.MaxResponseBodyBytes;
        _runtime.Policy.AllowedMethods.Clear();
        foreach (var method in policy.AllowedMethods) _runtime.Policy.AllowedMethods.Add(method);
        _runtime.Policy.AllowedBaseUrls.Clear();
        foreach (var url in policy.AllowedBaseUrls) _runtime.Policy.AllowedBaseUrls.Add(url);
    }

    private static ApiExecutionPolicy Clone(ApiExecutionPolicy policy) => new()
    {
        DryRun = policy.DryRun,
        BlockLocalhost = policy.BlockLocalhost,
        BlockPrivateNetworks = policy.BlockPrivateNetworks,
        Timeout = policy.Timeout,
        MaxRequestBodyBytes = policy.MaxRequestBodyBytes,
        MaxResponseBodyBytes = policy.MaxResponseBodyBytes,
        AllowedMethods = new HashSet<string>(policy.AllowedMethods, StringComparer.OrdinalIgnoreCase),
        AllowedBaseUrls = policy.AllowedBaseUrls.ToList()
    };

    private static int RequireNonNegative(int value, string name)
        => value >= 0 ? value : throw new InvalidOperationException($"{name} must be >= 0.");
}
