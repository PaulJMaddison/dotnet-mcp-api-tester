using System.ComponentModel;
using System.Text.Json;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class PolicyTools
{
    private readonly ApiRuntimeConfig _cfg;

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

    [McpServerTool, Description("Update the API execution policy. Pass policyJson as a JSON object string.")]
    public object ApiSetPolicy(string policyJson)
    {
        using var doc = JsonDocument.Parse(policyJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("dryRun", out var dryRun))
            _cfg.Policy.DryRun = dryRun.GetBoolean();

        if (root.TryGetProperty("allowedMethods", out var methods) && methods.ValueKind == JsonValueKind.Array)
        {
            _cfg.Policy.AllowedMethods.Clear();
            foreach (var m in methods.EnumerateArray())
                _cfg.Policy.AllowedMethods.Add(m.GetString() ?? "");
        }

        if (root.TryGetProperty("allowedBaseUrls", out var urls) && urls.ValueKind == JsonValueKind.Array)
        {
            _cfg.Policy.AllowedBaseUrls.Clear();
            foreach (var u in urls.EnumerateArray())
                _cfg.Policy.AllowedBaseUrls.Add(u.GetString() ?? "");
        }

        if (root.TryGetProperty("timeoutSeconds", out var timeoutSeconds))
            _cfg.Policy.Timeout = TimeSpan.FromSeconds(timeoutSeconds.GetInt32());

        if (root.TryGetProperty("maxRequestBodyBytes", out var maxReq))
            _cfg.Policy.MaxRequestBodyBytes = maxReq.GetInt32();

        if (root.TryGetProperty("maxResponseBodyBytes", out var maxResp))
            _cfg.Policy.MaxResponseBodyBytes = maxResp.GetInt32();

        if (root.TryGetProperty("blockLocalhost", out var blockLocalhost))
            _cfg.Policy.BlockLocalhost = blockLocalhost.GetBoolean();

        if (root.TryGetProperty("blockPrivateNetworks", out var blockPrivate))
            _cfg.Policy.BlockPrivateNetworks = blockPrivate.GetBoolean();

        return new { ok = true, policy = ApiGetPolicy() };
    }
}
