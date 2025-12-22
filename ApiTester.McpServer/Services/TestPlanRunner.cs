using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ApiTester.McpServer.Models;
using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed class TestPlanRunner
{
    private readonly OpenApiStore _store;
    private readonly ApiRuntimeConfig _cfg;
    private readonly IHttpClientFactory _httpClientFactory;

    public TestPlanRunner(OpenApiStore store, ApiRuntimeConfig cfg, IHttpClientFactory httpClientFactory)
    {
        _store = store;
        _cfg = cfg;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TestRunResult> RunAsync(string operationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        var doc = _store.RequireDocument();
        var match = FindOperation(doc, operationId);

        if (match is null)
            throw new InvalidOperationException($"operationId not found in OpenAPI: {operationId}");

        var (path, method, op) = match.Value;

        var plan = TestPlanFactory.Create(op, method, path, operationId);

        var swTotal = Stopwatch.StartNew();
        var results = new List<TestCaseResult>();

        foreach (var tc in plan.Cases)
        {
            results.Add(await RunCaseAsync(plan, tc, ct));
        }

        swTotal.Stop();

        var blocked = results.Count(x => x.Blocked);
        var passed = results.Count(x => !x.Blocked && x.Pass);
        var failed = results.Count(x => !x.Blocked && !x.Pass);

        return new TestRunResult
        {
            OperationId = operationId,
            TotalCases = results.Count,
            Passed = passed,
            Failed = failed,
            Blocked = blocked,
            TotalDurationMs = swTotal.ElapsedMilliseconds,
            Results = results
        };
    }

    private async Task<TestCaseResult> RunCaseAsync(TestPlan plan, TestCase tc, CancellationToken ct)
    {
        // If URL template has required params but tc didn’t supply them, mark blocked deterministically
        var missing = ExtractPathParamNames(plan.PathTemplate)
            .Where(p => !tc.PathParams.ContainsKey(p))
            .ToList();

        if (missing.Count > 0)
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = $"Missing required path param(s): {string.Join(", ", missing)}",
                Method = plan.Method,
                Pass = false
            };
        }

        // Build URL
        var baseUrl = (_cfg.BaseUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = "No baseUrl configured. Call api_set_base_url first.",
                Method = plan.Method,
                Pass = false
            };
        }

        var path = ApplyPathParams(plan.PathTemplate, tc.PathParams);
        var full = baseUrl + path;

        if (tc.QueryParams.Count > 0)
        {
            var qs = string.Join("&", tc.QueryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            full += "?" + qs;
        }

        if (!Uri.TryCreate(full, UriKind.Absolute, out var uri))
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = $"Built URL is invalid: {full}",
                Method = plan.Method,
                Pass = false
            };
        }

        // Policy checks
        var policyBlock = PolicyBlockReason(uri, plan.Method);
        if (policyBlock is not null)
        {
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = true,
                BlockReason = policyBlock,
                Method = plan.Method,
                Url = uri.ToString(),
                Pass = false
            };
        }

        // Execute
        var client = _httpClientFactory.CreateClient();
        using var req = new HttpRequestMessage(new HttpMethod(plan.Method), uri);

        // bearer
        if (!string.IsNullOrWhiteSpace(_cfg.BearerToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cfg.BearerToken);

        // headers
        foreach (var h in tc.Headers)
            req.Headers.TryAddWithoutValidation(h.Key, h.Value);

        // body (not used for GET but leaving here for later)
        if (!string.IsNullOrWhiteSpace(tc.BodyJson))
        {
            req.Content = new StringContent(tc.BodyJson, Encoding.UTF8, "application/json");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

            var status = (int)resp.StatusCode;

            string? snippet = null;
            if (resp.Content is not null)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                var max = Math.Min(bytes.Length, _cfg.Policy.MaxResponseBodyBytes);
                snippet = Encoding.UTF8.GetString(bytes, 0, max);
                if (bytes.Length > max) snippet += "\n... (truncated)";
            }

            var expected = tc.ExpectedStatusCodes.Count > 0 ? tc.ExpectedStatusCodes : new List<int> { 200 };
            var pass = expected.Contains(status);

            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = false,
                Method = plan.Method,
                Url = uri.ToString(),
                StatusCode = status,
                DurationMs = sw.ElapsedMilliseconds,
                Pass = pass,
                FailureReason = pass ? null : $"Expected [{string.Join(", ", expected)}] but got {status}.",
                ResponseSnippet = snippet
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestCaseResult
            {
                Name = tc.Name,
                Blocked = false,
                Method = plan.Method,
                Url = uri.ToString(),
                DurationMs = sw.ElapsedMilliseconds,
                Pass = false,
                FailureReason = ex.Message
            };
        }
    }

    private string? PolicyBlockReason(Uri uri, string method)
    {
        // deny-by-default on baseUrl allow list
        var allow = _cfg.Policy.AllowedBaseUrls ?? new List<string>();
        if (allow.Count == 0 && !_cfg.Policy.DryRun)
            return "No allowedBaseUrls configured, deny by default.";

        // method allow list
        if (_cfg.Policy.AllowedMethods is not null &&
            _cfg.Policy.AllowedMethods.Count > 0 &&
            !_cfg.Policy.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return $"HTTP method not allowed by policy: {method}";
        }

        // base url allow list (when present)
        if (allow.Count > 0)
        {
            var u = uri.ToString().TrimEnd('/');
            var ok = allow.Any(a =>
            {
                var aa = (a ?? "").Trim().TrimEnd('/');
                return u.StartsWith(aa, StringComparison.OrdinalIgnoreCase);
            });

            if (!ok)
                return $"Base URL not allowed by policy: {uri.GetLeftPart(UriPartial.Authority)}";
        }

        // SSRF guards
        if (_cfg.Policy.BlockLocalhost || _cfg.Policy.BlockPrivateNetworks)
        {
            var ssrf = SsrfBlockReason(uri, _cfg.Policy.BlockLocalhost, _cfg.Policy.BlockPrivateNetworks);
            if (ssrf is not null)
                return ssrf;
        }

        return null;
    }

    private static string? SsrfBlockReason(Uri uri, bool blockLocalhost, bool blockPrivate)
    {
        // fast hostname checks
        var host = uri.Host;

        if (blockLocalhost)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                return "Localhost blocked";

            if (string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return "Loopback IPv4 blocked";

            if (string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
                return "Loopback IPv6 blocked";
        }

        // If it’s an IP literal we can range-check without DNS
        if (IPAddress.TryParse(host, out var ip))
        {
            return IpBlockReason(ip, blockLocalhost, blockPrivate);
        }

        // For now: no DNS resolution (keeps deterministic + avoids delays)
        // If you want DNS resolution later, we can add it behind a policy flag.
        return null;
    }

    private static string? IpBlockReason(IPAddress ip, bool blockLocalhost, bool blockPrivate)
    {
        if (blockLocalhost)
        {
            if (IPAddress.IsLoopback(ip))
                return "Loopback IP blocked";
        }

        if (blockPrivate)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                // 10.0.0.0/8
                if (b[0] == 10) return "Private IPv4 (10.0.0.0/8)";
                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return "Private IPv4 (172.16.0.0/12)";
                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168) return "Private IPv4 (192.168.0.0/16)";
                // 169.254.0.0/16 link-local (metadata range)
                if (b[0] == 169 && b[1] == 254) return "Link-local IPv4 (includes metadata endpoint range)";
            }
            else
            {
                // IPv6 link-local fe80::/10
                var b = ip.GetAddressBytes();
                if (b.Length > 0 && b[0] == 0xFE && (b[1] & 0xC0) == 0x80)
                    return "Link-local IPv6 (fe80::/10)";
            }
        }

        return null;
    }

    private static (string path, OperationType method, OpenApiOperation op)? FindOperation(OpenApiDocument doc, string operationId)
    {
        foreach (var path in doc.Paths)
        {
            foreach (var kv in path.Value.Operations)
            {
                var op = kv.Value;
                if (string.Equals(op.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                    return (path.Key, kv.Key, op);
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractPathParamNames(string pathTemplate)
    {
        // "/status/{code}" -> ["code"]
        var names = new List<string>();
        var i = 0;
        while (i < pathTemplate.Length)
        {
            var start = pathTemplate.IndexOf('{', i);
            if (start < 0) break;
            var end = pathTemplate.IndexOf('}', start + 1);
            if (end < 0) break;

            var name = pathTemplate.Substring(start + 1, end - start - 1);
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);

            i = end + 1;
        }

        return names;
    }

    private static string ApplyPathParams(string pathTemplate, Dictionary<string, string> pathParams)
    {
        var result = pathTemplate;
        foreach (var kv in pathParams)
        {
            result = result.Replace("{" + kv.Key + "}", Uri.EscapeDataString(kv.Value), StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
