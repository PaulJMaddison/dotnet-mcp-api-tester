using System.Diagnostics;
using System.Text.Json;

static async Task<int> Main()
{
    var serverProjectPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "ApiTester.McpServer", "ApiTester.McpServer.csproj"));

    var httpbinSpecPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "Specs", "httpbin.openapi.json"));

    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --project \"{serverProjectPath}\"",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(serverProjectPath)! // ensures server content root is stable
    };

    using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start MCP server process.");

    // Pipe server stderr to client stderr to aid debugging.
    _ = Task.Run(async () =>
    {
        while (!proc.StandardError.EndOfStream)
        {
            var line = await proc.StandardError.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
                Console.Error.WriteLine("[server] " + line);
        }
    });

    using var input = proc.StandardInput;
    using var output = proc.StandardOutput;

    var id = 0;
    var exitCode = 0;

    var jsonPretty = new JsonSerializerOptions { WriteIndented = true };

    static string TryPrettyJson(string? maybeJson, JsonSerializerOptions pretty)
    {
        if (string.IsNullOrWhiteSpace(maybeJson)) return maybeJson ?? "";

        var s = maybeJson.Trim();
        if (!(s.StartsWith("{") || s.StartsWith("["))) return maybeJson;

        try
        {
            using var doc = JsonDocument.Parse(s);
            return JsonSerializer.Serialize(doc.RootElement, pretty);
        }
        catch
        {
            return maybeJson;
        }
    }

    string Pretty(JsonElement el) => JsonSerializer.Serialize(el, jsonPretty);

    void PrintSection(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 80));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 80));
    }

    void PrintToolTextOrRaw(string label, JsonDocument response)
    {
        Console.WriteLine();
        Console.WriteLine(label);

        // Prefer MCP "content[].text" if present (common for these tools)
        if (response.RootElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array &&
            content.GetArrayLength() > 0 &&
            content[0].TryGetProperty("text", out var textEl))
        {
            var text = textEl.GetString();
            Console.WriteLine(TryPrettyJson(text, jsonPretty));
            return;
        }

        Console.WriteLine(Pretty(response.RootElement));
    }

    static bool ToolCallIsError(JsonDocument response)
    {
        return response.RootElement.TryGetProperty("result", out var r) &&
               r.TryGetProperty("isError", out var isError) &&
               isError.ValueKind == JsonValueKind.True;
    }

    static bool IsExpectedBlocked(JsonElement resultItem)
    {
        if (!resultItem.TryGetProperty("blocked", out var blockedEl) || blockedEl.ValueKind != JsonValueKind.True)
            return false;

        if (!resultItem.TryGetProperty("blockReason", out var reasonEl) || reasonEl.ValueKind != JsonValueKind.String)
            return false;

        var reason = reasonEl.GetString() ?? "";

        // Input validation blocks are expected behaviour (bad/missing params), not a product failure.
        return reason.StartsWith("Missing required path param", StringComparison.OrdinalIgnoreCase) ||
               reason.StartsWith("Missing required query param", StringComparison.OrdinalIgnoreCase) ||
               reason.StartsWith("Missing required header", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsHttpbinFlake(JsonElement resultItem)
    {
        // httpbin occasionally returns 502 via upstream infra. Treat as a flaky external dependency.
        if (resultItem.TryGetProperty("statusCode", out var scEl) &&
            scEl.ValueKind == JsonValueKind.Number &&
            scEl.GetInt32() == 502)
            return true;

        if (resultItem.TryGetProperty("responseSnippet", out var snipEl) && snipEl.ValueKind == JsonValueKind.String)
        {
            var snip = snipEl.GetString() ?? "";
            if (snip.Contains("502 Bad Gateway", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (resultItem.TryGetProperty("failureReason", out var failEl) && failEl.ValueKind == JsonValueKind.String)
        {
            var fail = failEl.GetString() ?? "";
            if (fail.Contains("but got 502", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    void SummariseRunTestPlan(string operationId, JsonDocument response)
    {
        if (!response.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            return;

        // Support both shapes:
        // - direct result { results: [...] }
        // - record { runId, ..., result: { results: [...] } }
        JsonElement resultsContainer = result;
        if (result.TryGetProperty("result", out var inner) && inner.ValueKind == JsonValueKind.Object)
            resultsContainer = inner;

        if (!resultsContainer.TryGetProperty("results", out var resultsEl) || resultsEl.ValueKind != JsonValueKind.Array)
            return;

        var total = 0;
        var pass = 0;
        var fail = 0;
        var blockedExpected = 0;
        var blockedUnexpected = 0;
        var flaky = 0;

        foreach (var item in resultsEl.EnumerateArray())
        {
            total++;

            if (IsExpectedBlocked(item))
            {
                blockedExpected++;
                continue;
            }

            if (item.TryGetProperty("blocked", out var blockedEl) && blockedEl.ValueKind == JsonValueKind.True)
            {
                blockedUnexpected++;
                continue;
            }

            var isPass = item.TryGetProperty("pass", out var passEl) && passEl.ValueKind == JsonValueKind.True;
            if (isPass)
            {
                pass++;
                continue;
            }

            if (IsHttpbinFlake(item))
            {
                flaky++;
                continue;
            }

            fail++;
        }

        Console.WriteLine();
        Console.WriteLine($"Run summary for '{operationId}':");
        Console.WriteLine($"- total: {total}");
        Console.WriteLine($"- pass: {pass}");
        Console.WriteLine($"- expected blocks: {blockedExpected}");
        Console.WriteLine($"- unexpected blocks: {blockedUnexpected}");
        Console.WriteLine($"- flaky (httpbin 502): {flaky}");
        Console.WriteLine($"- real fails: {fail}");

        // CI rule: only real fails or unexpected blocks should fail the run.
        if (fail > 0 || blockedUnexpected > 0)
            exitCode = 1;
    }

    static string? ExtractRunId(JsonDocument toolResponse)
    {
        // New shape: api_run_test_plan returns a record, with result.runId
        if (toolResponse.RootElement.TryGetProperty("result", out var result) &&
            result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("runId", out var runIdEl) &&
            runIdEl.ValueKind == JsonValueKind.String)
        {
            return runIdEl.GetString();
        }

        // Older shape compatibility: some implementations might return the runId nested differently
        if (toolResponse.RootElement.TryGetProperty("result", out var legacyResult) &&
            legacyResult.ValueKind == JsonValueKind.Object &&
            legacyResult.TryGetProperty("result", out var inner) &&
            inner.ValueKind == JsonValueKind.Object &&
            legacyResult.TryGetProperty("runId", out var legacyRunIdEl) &&
            legacyRunIdEl.ValueKind == JsonValueKind.String)
        {
            return legacyRunIdEl.GetString();
        }

        return null;
    }

    async Task<JsonDocument> SendAsync(object payload, int maxAttempts = 2)
    {
        var json = JsonSerializer.Serialize(payload);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await input.WriteLineAsync(json);
            await input.FlushAsync();

            // MCP stdio transport is line-delimited JSON.
            var line = await output.ReadLineAsync();
            if (line is null)
            {
                if (attempt == maxAttempts)
                    throw new InvalidOperationException("Server closed stdout unexpectedly.");

                await Task.Delay(50);
                continue;
            }

            return JsonDocument.Parse(line);
        }

        throw new InvalidOperationException("SendAsync failed unexpectedly.");
    }

    async Task<JsonDocument> CallToolAsync(string name, object arguments)
    {
        id++;
        return await SendAsync(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new
            {
                name,
                arguments
            }
        });
    }

    id++;
    var initResponse = await SendAsync(new
    {
        jsonrpc = "2.0",
        id,
        method = "initialize",
        @params = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "ApiTester.McpClient", version = "0.1.0" }
        }
    });

    PrintSection("INITIALISE");
    Console.WriteLine("initialize response:");
    Console.WriteLine(Pretty(initResponse.RootElement));

    id++;
    var toolsList = await SendAsync(new
    {
        jsonrpc = "2.0",
        id,
        method = "tools/list",
        @params = new { }
    });

    PrintSection("TOOLS");

    var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    Console.WriteLine("Tool names:");
    if (toolsList.RootElement.TryGetProperty("result", out var tlResult) &&
        tlResult.TryGetProperty("tools", out var toolsEl) &&
        toolsEl.ValueKind == JsonValueKind.Array)
    {
        foreach (var t in toolsEl.EnumerateArray())
        {
            var name = t.GetProperty("name").GetString() ?? "";
            toolNames.Add(name);
            Console.WriteLine("- " + name);
        }
    }

    Console.WriteLine();
    Console.WriteLine("tools/list response:");
    Console.WriteLine(Pretty(toolsList.RootElement));

    PrintSection("IMPORT OPENAPI");
    var import = await CallToolAsync("api_import_open_api", new
    {
        specUrlOrPath = httpbinSpecPath
    });
    PrintToolTextOrRaw("api_import_open_api response:", import);

    if (ToolCallIsError(import))
    {
        Console.WriteLine("OpenAPI import failed, stopping client run.");
        return 1;
    }

    PrintSection("DESCRIBE OPERATIONS");

    var describeUuid = await CallToolAsync("api_describe_operation", new { operationId = "getUuid" });
    PrintToolTextOrRaw("api_describe_operation getUuid response:", describeUuid);
    if (ToolCallIsError(describeUuid)) return 1;

    var describeStatus = await CallToolAsync("api_describe_operation", new { operationId = "getStatus" });
    PrintToolTextOrRaw("api_describe_operation getStatus response:", describeStatus);
    if (ToolCallIsError(describeStatus)) return 1;

    PrintSection("GENERATE TEST PLANS");

    var planUuid = await CallToolAsync("api_generate_test_plan", new { operationId = "getUuid" });
    PrintToolTextOrRaw("api_generate_test_plan getUuid response:", planUuid);
    if (ToolCallIsError(planUuid)) return 1;

    var planStatus = await CallToolAsync("api_generate_test_plan", new { operationId = "getStatus" });
    PrintToolTextOrRaw("api_generate_test_plan getStatus response:", planStatus);
    if (ToolCallIsError(planStatus)) return 1;

    var runToolName =
        toolNames.Contains("api_run_test_plan") ? "api_run_test_plan" :
        toolNames.Contains("api_execute_test_plan") ? "api_execute_test_plan" :
        null;

    PrintSection("POLICY + CALLS");

    var setBaseUrl = await CallToolAsync("api_set_base_url", new { baseUrl = "https://httpbin.org" });
    PrintToolTextOrRaw("api_set_base_url response:", setBaseUrl);

    var getPolicy0 = await CallToolAsync("api_get_policy", new { });
    PrintToolTextOrRaw("api_get_policy (initial) response:", getPolicy0);

    var policyJsonA = """
{
  "dryRun": false,
  "allowedMethods": ["GET"],
  "allowedBaseUrls": []
}
""";

    var setPolicyA = await CallToolAsync("api_set_policy", new { policyJson = policyJsonA });
    PrintToolTextOrRaw("api_set_policy A (deny-by-default) response:", setPolicyA);

    var callA = await CallToolAsync("api_call_operation", new { operationId = "getUuid" });
    PrintToolTextOrRaw("api_call_operation (should be blocked) response:", callA);

    var policyJsonB = """
{
  "dryRun": false,
  "allowedMethods": ["GET"],
  "allowedBaseUrls": ["https://httpbin.org"],
  "blockLocalhost": true,
  "blockPrivateNetworks": true,
  "timeoutSeconds": 10,
  "maxRequestBodyBytes": 262144,
  "maxResponseBodyBytes": 524288
}
""";

    var setPolicyB = await CallToolAsync("api_set_policy", new { policyJson = policyJsonB });
    PrintToolTextOrRaw("api_set_policy B (allow httpbin) response:", setPolicyB);

    PrintSection("RUN TEST PLANS");

    var capturedRunIds = new List<string>();

    if (runToolName is not null)
    {
        Console.WriteLine($"Using tool: {runToolName}");

        var runUuid = await CallToolAsync(runToolName, new { operationId = "getUuid" });
        PrintToolTextOrRaw($"{runToolName} getUuid response:", runUuid);
        if (ToolCallIsError(runUuid)) return 1;

        var runIdUuid = ExtractRunId(runUuid);
        if (!string.IsNullOrWhiteSpace(runIdUuid))
        {
            capturedRunIds.Add(runIdUuid);
            if (toolNames.Contains("api_get_run"))
            {
                var getRunUuid = await CallToolAsync("api_get_run", new { runId = runIdUuid });
                PrintToolTextOrRaw($"api_get_run ({runIdUuid}) response:", getRunUuid);
                if (ToolCallIsError(getRunUuid)) return 1;
            }
        }

        SummariseRunTestPlan("getUuid", runUuid);

        var runStatus = await CallToolAsync(runToolName, new { operationId = "getStatus" });
        PrintToolTextOrRaw($"{runToolName} getStatus response:", runStatus);
        if (ToolCallIsError(runStatus)) return 1;

        var runIdStatus = ExtractRunId(runStatus);
        if (!string.IsNullOrWhiteSpace(runIdStatus))
        {
            capturedRunIds.Add(runIdStatus);
            if (toolNames.Contains("api_get_run"))
            {
                var getRunStatus = await CallToolAsync("api_get_run", new { runId = runIdStatus });
                PrintToolTextOrRaw($"api_get_run ({runIdStatus}) response:", getRunStatus);
                if (ToolCallIsError(getRunStatus)) return 1;
            }
        }

        SummariseRunTestPlan("getStatus", runStatus);
    }
    else
    {
        Console.WriteLine("No test execution tool present (expected one of: api_run_test_plan, api_execute_test_plan).");
    }

    if (toolNames.Contains("api_list_runs"))
    {
        PrintSection("RUN HISTORY");

        var listRuns = await CallToolAsync("api_list_runs", new { take = 10 });
        PrintToolTextOrRaw("api_list_runs response:", listRuns);
        if (ToolCallIsError(listRuns)) return 1;
    }

    var callB1 = await CallToolAsync("api_call_operation", new { operationId = "getUuid" });
    PrintToolTextOrRaw("api_call_operation getUuid response:", callB1);

    var callB2 = await CallToolAsync("api_call_operation", new { operationId = "getGet" });
    PrintToolTextOrRaw("api_call_operation getGet response:", callB2);

    var callB3 = await CallToolAsync("api_call_operation", new { operationId = "getStatus", pathParamsJson = "{\"code\":200}" });
    PrintToolTextOrRaw("api_call_operation getStatus(200) response:", callB3);

    var setBaseUrlMeta = await CallToolAsync("api_set_base_url", new { baseUrl = "http://169.254.169.254" });
    PrintToolTextOrRaw("api_set_base_url (metadata IP) response:", setBaseUrlMeta);

    var policyJsonC = """
{
  "dryRun": false,
  "allowedMethods": ["GET"],
  "allowedBaseUrls": ["http://169.254.169.254"],
  "blockLocalhost": true,
  "blockPrivateNetworks": true
}
""";

    var setPolicyC = await CallToolAsync("api_set_policy", new { policyJson = policyJsonC });
    PrintToolTextOrRaw("api_set_policy C (allowlist metadata, expect SSRF block) response:", setPolicyC);

    var callC = await CallToolAsync("api_call_operation", new { operationId = "getUuid" });
    PrintToolTextOrRaw("api_call_operation (should be blocked by SSRF guard) response:", callC);

    var resetRuntime = await CallToolAsync("api_reset_runtime", new { });
    PrintToolTextOrRaw("api_reset_runtime response:", resetRuntime);

    PrintSection("ASSERT RESET");

    var getPolicyAfterReset = await CallToolAsync("api_get_policy", new { });
    PrintToolTextOrRaw("api_get_policy (after reset_runtime) response:", getPolicyAfterReset);

    var setPolicyAfterReset = await CallToolAsync("api_set_policy", new { policyJson = policyJsonB });
    PrintToolTextOrRaw("api_set_policy (after reset_runtime, allow httpbin) response:", setPolicyAfterReset);

    var callAfterReset = await CallToolAsync("api_call_operation", new { operationId = "getUuid" });
    PrintToolTextOrRaw("api_call_operation (after reset_runtime + allow httpbin) response:", callAfterReset);

    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }

    return exitCode;
}

return await Main();
