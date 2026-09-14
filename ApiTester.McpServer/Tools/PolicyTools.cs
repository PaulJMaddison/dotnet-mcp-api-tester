using System.ComponentModel;
using System.Text.Json;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class PolicyTools
{
    private readonly ApiRuntimeConfig _cfg;
    private readonly McpSafetyOptions _safety;

    public PolicyTools(ApiRuntimeConfig cfg, McpSafetyOptions? safety = null)
    {
        _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        _safety = safety ?? new McpSafetyOptions(false);
    }

    [McpServerTool, Description("Get the current in-memory API execution policy and whether this process permits MCP policy mutation.")]
    public object ApiGetPolicy() => new
    {
        dryRun = _cfg.Policy.DryRun,
        allowedBaseUrls = _cfg.Policy.AllowedBaseUrls,
        allowedMethods = _cfg.Policy.AllowedMethods.ToArray(),
        timeoutSeconds = (int)_cfg.Policy.Timeout.TotalSeconds,
        maxRequestBodyBytes = _cfg.Policy.MaxRequestBodyBytes,
        maxResponseBodyBytes = _cfg.Policy.MaxResponseBodyBytes,
        validateSchema = _cfg.Policy.ValidateSchema,
        blockLocalhost = _cfg.Policy.BlockLocalhost,
        blockPrivateNetworks = _cfg.Policy.BlockPrivateNetworks,
        retryOnFlake = _cfg.Policy.RetryOnFlake,
        maxRetries = _cfg.Policy.MaxRetries,
        mcpPolicyMutationEnabled = _safety.AllowPolicyMutation,
        persistence = "none"
    };

    [McpServerTool, Description("Reset execution policy to safe deny-by-default dry-run defaults.")]
    public object ApiResetPolicy()
    {
        ApiPolicyDefaults.ApplySafeDefaults(_cfg.Policy);
        return new { ok = true, policy = ApiGetPolicy() };
    }

    [McpServerTool, Description("Clear runtime base URL and auth and reset policy to safe defaults.")]
    public object ApiResetRuntime()
    {
        _cfg.ResetRuntime();
        return new
        {
            ok = true,
            baseUrl = _cfg.BaseUrl,
            bearerToken = _cfg.BearerToken,
            policy = ApiGetPolicy()
        };
    }

    [McpServerTool, Description("Update the in-memory execution policy. Disabled by default unless a human enables policy mutation when starting the MCP server.")]
    public object ApiSetPolicy(string policyJson)
    {
        if (!_safety.AllowPolicyMutation)
        {
            return new
            {
                isError = true,
                error = "MCP policy mutation is disabled. Set APITESTER_MCP_ALLOW_POLICY_MUTATION=true before starting the server only when you intentionally want the connected agent to change execution policy."
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(policyJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("policyJson must be a JSON object.");

            var next = ClonePolicy(_cfg.Policy);

            if (root.TryGetProperty("dryRun", out var dryRun))
                next.DryRun = dryRun.GetBoolean();

            if (root.TryGetProperty("allowedMethods", out var methods) && methods.ValueKind == JsonValueKind.Array)
            {
                next.AllowedMethods.Clear();
                foreach (var method in methods.EnumerateArray())
                {
                    var value = (method.GetString() ?? string.Empty).Trim().ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(value)) next.AllowedMethods.Add(value);
                }
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
                        throw new InvalidOperationException($"Invalid allowedBaseUrl: {value}. Must be absolute http/https URL.");
                    next.AllowedBaseUrls.Add(value);
                }
            }

            if (root.TryGetProperty("timeoutSeconds", out var timeoutSeconds))
                next.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds.GetInt32(), 1, 60));

            if (root.TryGetProperty("maxRequestBodyBytes", out var maxReq))
            {
                var value = maxReq.GetInt32();
                if (value < 0) throw new InvalidOperationException("maxRequestBodyBytes must be >= 0.");
                next.MaxRequestBodyBytes = value;
            }

            if (root.TryGetProperty("maxResponseBodyBytes", out var maxResp))
            {
                var value = maxResp.GetInt32();
                if (value < 0) throw new InvalidOperationException("maxResponseBodyBytes must be >= 0.");
                next.MaxResponseBodyBytes = value;
            }

            if (root.TryGetProperty("validateSchema", out var validateSchema)) next.ValidateSchema = validateSchema.GetBoolean();
            if (root.TryGetProperty("blockLocalhost", out var blockLocalhost)) next.BlockLocalhost = blockLocalhost.GetBoolean();
            if (root.TryGetProperty("blockPrivateNetworks", out var blockPrivate)) next.BlockPrivateNetworks = blockPrivate.GetBoolean();
            if (root.TryGetProperty("retryOnFlake", out var retryOnFlake)) next.RetryOnFlake = retryOnFlake.GetBoolean();

            if (root.TryGetProperty("maxRetries", out var maxRetries))
            {
                var value = maxRetries.GetInt32();
                if (value < 0) throw new InvalidOperationException("maxRetries must be >= 0.");
                next.MaxRetries = value;
            }

            if (next.AllowedMethods.Count == 0) next.AllowedMethods.Add("GET");
            ApplyPolicy(next);
            return new { ok = true, policy = ApiGetPolicy() };
        }
        catch (Exception ex)
        {
            return new { isError = true, error = ex.Message };
        }
    }

    private void ApplyPolicy(ApiExecutionPolicy policy)
    {
        _cfg.Policy.DryRun = policy.DryRun;
        _cfg.Policy.BlockLocalhost = policy.BlockLocalhost;
        _cfg.Policy.BlockPrivateNetworks = policy.BlockPrivateNetworks;
        _cfg.Policy.Timeout = policy.Timeout;
        _cfg.Policy.MaxRequestBodyBytes = policy.MaxRequestBodyBytes;
        _cfg.Policy.MaxResponseBodyBytes = policy.MaxResponseBodyBytes;
        _cfg.Policy.ValidateSchema = policy.ValidateSchema;
        _cfg.Policy.RetryOnFlake = policy.RetryOnFlake;
        _cfg.Policy.MaxRetries = policy.MaxRetries;

        _cfg.Policy.AllowedMethods.Clear();
        foreach (var method in policy.AllowedMethods) _cfg.Policy.AllowedMethods.Add(method);

        _cfg.Policy.AllowedBaseUrls.Clear();
        foreach (var url in policy.AllowedBaseUrls) _cfg.Policy.AllowedBaseUrls.Add(url);
    }

    private static ApiExecutionPolicy ClonePolicy(ApiExecutionPolicy p) => new()
    {
        DryRun = p.DryRun,
        AllowedBaseUrls = p.AllowedBaseUrls.ToList(),
        BlockLocalhost = p.BlockLocalhost,
        BlockPrivateNetworks = p.BlockPrivateNetworks,
        AllowedMethods = new HashSet<string>(p.AllowedMethods, StringComparer.OrdinalIgnoreCase),
        Timeout = p.Timeout,
        MaxRequestBodyBytes = p.MaxRequestBodyBytes,
        MaxResponseBodyBytes = p.MaxResponseBodyBytes,
        ValidateSchema = p.ValidateSchema,
        RetryOnFlake = p.RetryOnFlake,
        MaxRetries = p.MaxRetries
    };
}
