using System.ComponentModel;
using System.Text.Json;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class PolicyTools
{
    private readonly ApiRuntimeConfig _cfg;

    // Central “safe defaults” for the product
    private static readonly ApiExecutionPolicy SafeDefaults = new()
    {
        DryRun = true,
        AllowedMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET" },
        AllowedBaseUrls = new List<string>(),
        BlockLocalhost = true,
        BlockPrivateNetworks = true,
        Timeout = TimeSpan.FromSeconds(10),
        MaxRequestBodyBytes = 262_144,
        MaxResponseBodyBytes = 524_288
    };

    public PolicyTools(ApiRuntimeConfig cfg)
    {
        _cfg = cfg;
    }

    [McpServerTool, Description("Get the current API execution policy.")]
    public object ApiGetPolicy()
    {
        return new
        {
            dryRun = _cfg.Policy.DryRun,
            allowedBaseUrls = _cfg.Policy.AllowedBaseUrls,
            allowedMethods = _cfg.Policy.AllowedMethods.ToArray(),
            timeoutSeconds = (int)_cfg.Policy.Timeout.TotalSeconds,
            maxRequestBodyBytes = _cfg.Policy.MaxRequestBodyBytes,
            maxResponseBodyBytes = _cfg.Policy.MaxResponseBodyBytes,
            blockLocalhost = _cfg.Policy.BlockLocalhost,
            blockPrivateNetworks = _cfg.Policy.BlockPrivateNetworks
        };
    }

    [McpServerTool, Description("Reset the API execution policy to safe defaults (deny-by-default + dryRun).")]
    public object ApiResetPolicy()
    {
        ApplyPolicy(SafeDefaults);
        return new { ok = true, policy = ApiGetPolicy() };
    }

    [McpServerTool, Description("Update the API execution policy. Pass policyJson as a JSON object string.")]
    public object ApiSetPolicy(string policyJson)
    {
        using var doc = JsonDocument.Parse(policyJson);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("policyJson must be a JSON object.");

        // Start from current policy, patch fields from JSON
        var next = ClonePolicy(_cfg.Policy);

        if (root.TryGetProperty("dryRun", out var dryRun))
            next.DryRun = dryRun.GetBoolean();

        if (root.TryGetProperty("allowedMethods", out var methods) && methods.ValueKind == JsonValueKind.Array)
        {
            next.AllowedMethods.Clear();
            foreach (var m in methods.EnumerateArray())
            {
                var s = (m.GetString() ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(s))
                    next.AllowedMethods.Add(s);
            }
        }

        if (root.TryGetProperty("allowedBaseUrls", out var urls) && urls.ValueKind == JsonValueKind.Array)
        {
            next.AllowedBaseUrls.Clear();
            foreach (var u in urls.EnumerateArray())
            {
                var s = (u.GetString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(s))
                    continue;

                // normalise: trim + remove trailing slash
                s = s.TrimEnd('/');

                // basic validation: must be absolute http/https URI
                if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException($"Invalid allowedBaseUrl: {s}. Must be absolute http/https URL.");
                }

                next.AllowedBaseUrls.Add(s);
            }
        }

        if (root.TryGetProperty("timeoutSeconds", out var timeoutSeconds))
        {
            var seconds = timeoutSeconds.GetInt32();

            // clamp to sane range for now
            if (seconds < 1) seconds = 1;
            if (seconds > 60) seconds = 60;

            next.Timeout = TimeSpan.FromSeconds(seconds);
        }

        if (root.TryGetProperty("maxRequestBodyBytes", out var maxReq))
        {
            var v = maxReq.GetInt32();
            if (v < 0) throw new InvalidOperationException("maxRequestBodyBytes must be >= 0.");
            next.MaxRequestBodyBytes = v;
        }

        if (root.TryGetProperty("maxResponseBodyBytes", out var maxResp))
        {
            var v = maxResp.GetInt32();
            if (v < 0) throw new InvalidOperationException("maxResponseBodyBytes must be >= 0.");
            next.MaxResponseBodyBytes = v;
        }

        if (root.TryGetProperty("blockLocalhost", out var blockLocalhost))
            next.BlockLocalhost = blockLocalhost.GetBoolean();

        if (root.TryGetProperty("blockPrivateNetworks", out var blockPrivate))
            next.BlockPrivateNetworks = blockPrivate.GetBoolean();

        // Final sanity: if dryRun=false and allow list empty, that’s deny-by-default, which is fine.
        // But do not allow blank methods list, default to GET if cleared accidentally.
        if (next.AllowedMethods.Count == 0)
            next.AllowedMethods.Add("GET");

        ApplyPolicy(next);

        return new { ok = true, policy = ApiGetPolicy() };
    }

    private void ApplyPolicy(ApiExecutionPolicy policy)
    {
        _cfg.Policy.DryRun = policy.DryRun;
        _cfg.Policy.BlockLocalhost = policy.BlockLocalhost;
        _cfg.Policy.BlockPrivateNetworks = policy.BlockPrivateNetworks;
        _cfg.Policy.Timeout = policy.Timeout;
        _cfg.Policy.MaxRequestBodyBytes = policy.MaxRequestBodyBytes;
        _cfg.Policy.MaxResponseBodyBytes = policy.MaxResponseBodyBytes;

        _cfg.Policy.AllowedMethods.Clear();
        foreach (var m in policy.AllowedMethods)
            _cfg.Policy.AllowedMethods.Add(m);

        _cfg.Policy.AllowedBaseUrls.Clear();
        foreach (var u in policy.AllowedBaseUrls)
            _cfg.Policy.AllowedBaseUrls.Add(u);
    }

    private static ApiExecutionPolicy ClonePolicy(ApiExecutionPolicy p)
    {
        return new ApiExecutionPolicy
        {
            DryRun = p.DryRun,
            AllowedBaseUrls = p.AllowedBaseUrls.Select(x => x).ToList(),
            BlockLocalhost = p.BlockLocalhost,
            BlockPrivateNetworks = p.BlockPrivateNetworks,
            AllowedMethods = new HashSet<string>(p.AllowedMethods, StringComparer.OrdinalIgnoreCase),
            Timeout = p.Timeout,
            MaxRequestBodyBytes = p.MaxRequestBodyBytes,
            MaxResponseBodyBytes = p.MaxResponseBodyBytes
        };
    }
}
