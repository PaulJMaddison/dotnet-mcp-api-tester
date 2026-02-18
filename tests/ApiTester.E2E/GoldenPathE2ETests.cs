using Xunit;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace ApiTester.E2E;

[Trait("Category", "E2E")]
public class GoldenPathE2ETests : IAsyncLifetime
{
    private readonly string _siteBaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:18081";
    private readonly string _webBaseUrl = Environment.GetEnvironmentVariable("E2E_WEB_BASE_URL") ?? "http://localhost:18080";
    private readonly string _apiKey = Environment.GetEnvironmentVariable("E2E_API_KEY") ?? "dev-local-key";
    private readonly string _artifactRoot = Environment.GetEnvironmentVariable("E2E_ARTIFACTS_DIR") ?? Path.Combine("artifacts", "e2e", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_artifactRoot);
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact(DisplayName = "E2E-01 Login and account via DevBypass")]
    public async Task E2E01_LoginAndAccount()
    {
        await RunWithArtifactsAsync("e2e-01", async page =>
        {
            await page.GotoAsync($"{_siteBaseUrl}/app");
            await page.WaitForURLAsync("**/app/onboarding");
            await page.GotoAsync($"{_siteBaseUrl}/app/account");
            await page.GetByTestId("account-title").WaitForAsync();
            var account = await page.GetByTestId("account-card").InnerTextAsync();
            Assert.Contains("e2e@local.test", account, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact(DisplayName = "E2E-02 Token lifecycle")]
    public async Task E2E02_TokenLifecycle()
    {
        await RunWithArtifactsAsync("e2e-02", async page =>
        {
            await page.GotoAsync($"{_siteBaseUrl}/app/tokens");
            await page.GetByTestId("token-name-input").FillAsync("e2e-token");
            await page.GetByTestId("token-scopes-input").FillAsync("projects:read,projects:write,runs:read,runs:write");
            await page.GetByTestId("token-create-button").ClickAsync();
            await page.GetByTestId("token-created-value").WaitForAsync();
            var token = (await page.GetByTestId("token-created-value").InnerTextAsync()).Trim();
            Assert.NotEmpty(token);

            using var tokenClient = new HttpClient { BaseAddress = new Uri(_webBaseUrl) };
            tokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var before = await tokenClient.GetAsync("/api/projects");
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

            await page.GetByTestId("token-revoke-button").First.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            var after = await tokenClient.GetAsync("/api/projects");
            Assert.True(after.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        });
    }

    [Fact(DisplayName = "E2E-03 Project -> import spec -> plan -> run")]
    public async Task E2E03_ProjectRunGoldenPath()
    {
        var token = await CreateTokenAsync("e2e-run-token");
        using var client = CreateBearerClient(token);

        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "e2e-project" });
        projectResponse.EnsureSuccessStatusCode();
        var project = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync()).RootElement;
        var projectId = project.GetProperty("projectId").GetGuid();
        var projectKey = project.GetProperty("projectKey").GetString();

        using var form = new MultipartFormDataContent();
        var specPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "fixtures", "fixtureapi-openapi.json"));
        form.Add(new StringContent(await File.ReadAllTextAsync(specPath), Encoding.UTF8, "application/json"), "file", "fixtureapi-openapi.json");
        (await client.PostAsync($"/api/projects/{projectId}/specs/import", form)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/projects/{projectId}/testplans/getPing/generate", null)).EnsureSuccessStatusCode();

        var runResponse = await client.PostAsync($"/api/projects/{projectId}/runs/execute/getPing", null);
        runResponse.EnsureSuccessStatusCode();
        var runDoc = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync()).RootElement;
        var runId = runDoc.GetProperty("runId").GetGuid();

        await RunWithArtifactsAsync("e2e-03", async page =>
        {
            await page.GotoAsync($"{_siteBaseUrl}/app/projects/{projectKey}/runs");
            await page.GetByTestId("runs-table").WaitForAsync();
            var text = await page.GetByTestId("runs-table").InnerTextAsync();
            Assert.Contains("Passed", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(runId.ToString("N")[..8], text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact(DisplayName = "E2E-04 Hosted mode blocks egress until allowlist restored")]
    public async Task E2E04_HostedModeAllowlist()
    {
        var token = await CreateTokenAsync("e2e-policy-token");
        using var client = CreateBearerClient(token);
        var projectId = await CreateProjectAndSpecAsync(client, "e2e-policy-project");

        (await client.PutAsJsonAsync("/api/v1/runtime/policy", new { allowedBaseUrls = Array.Empty<string>() })).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/projects/{projectId}/testplans/getPing/generate", null)).EnsureSuccessStatusCode();
        var blockedRun = await client.PostAsync($"/api/projects/{projectId}/runs/execute/getPing", null);
        blockedRun.EnsureSuccessStatusCode();
        var blockedJson = JsonDocument.Parse(await blockedRun.Content.ReadAsStringAsync()).RootElement.GetRawText();
        Assert.Contains("HostedEgressDenied", blockedJson, StringComparison.OrdinalIgnoreCase);

        (await client.PutAsJsonAsync("/api/v1/runtime/policy", new { allowedBaseUrls = new[] { "http://fixtureapi:8080" } })).EnsureSuccessStatusCode();
        var rerun = await client.PostAsync($"/api/projects/{projectId}/runs/execute/getPing", null);
        rerun.EnsureSuccessStatusCode();
        var rerunJson = JsonDocument.Parse(await rerun.Content.ReadAsStringAsync()).RootElement.GetRawText();
        Assert.Contains("\"pass\":true", rerunJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "E2E-05 Evidence pack export is redacted")]
    public async Task E2E05_EvidencePackRedaction()
    {
        var token = await CreateTokenAsync("e2e-evidence-token");
        using var client = CreateBearerClient(token);
        var projectId = await CreateProjectAndSpecAsync(client, "e2e-evidence-project");
        (await client.PostAsync($"/api/projects/{projectId}/testplans/getPing/generate", null)).EnsureSuccessStatusCode();
        var run = await client.PostAsync($"/api/projects/{projectId}/runs/execute/getPing", null);
        run.EnsureSuccessStatusCode();
        var runId = JsonDocument.Parse(await run.Content.ReadAsStringAsync()).RootElement.GetProperty("runId").GetGuid();

        var evidence = await client.GetByteArrayAsync($"/runs/{runId}/export/evidence-bundle");
        var dir = Path.Combine(_artifactRoot, "e2e-05");
        Directory.CreateDirectory(dir);
        var zipPath = Path.Combine(dir, "evidence.zip");
        await File.WriteAllBytesAsync(zipPath, evidence);

        using var archive = ZipFile.OpenRead(zipPath);
        var names = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("manifest.json", names);
        Assert.Contains("run.json", names);
        Assert.Contains("policy-snapshot.json", names);

        var combined = new StringBuilder();
        foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(entry.Open());
            combined.AppendLine(await reader.ReadToEndAsync());
        }

        Assert.DoesNotContain("Authorization:", combined.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, combined.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "E2E-06 Billing not configured")]
    public async Task E2E06_BillingNotConfigured()
    {
        using var client = new HttpClient { BaseAddress = new Uri(_webBaseUrl) };
        client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);

        var response = await client.GetAsync("/api/v1/billing/plan");
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("BillingNotConfigured", payload, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> CreateTokenAsync(string name)
    {
        using var admin = new HttpClient { BaseAddress = new Uri(_webBaseUrl) };
        admin.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        var response = await admin.PostAsJsonAsync("/api/v1/tokens", new { name, scopes = new[] { "projects:read", "projects:write", "runs:read", "runs:write" } });
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return doc.GetProperty("token").GetString()!;
    }

    private HttpClient CreateBearerClient(string token)
    {
        var client = new HttpClient { BaseAddress = new Uri(_webBaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateProjectAndSpecAsync(HttpClient client, string projectName)
    {
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = projectName });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("projectId").GetGuid();

        var specPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "fixtures", "fixtureapi-openapi.json"));
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(await File.ReadAllTextAsync(specPath), Encoding.UTF8, "application/json"), "file", "fixtureapi-openapi.json");
        (await client.PostAsync($"/api/projects/{projectId}/specs/import", form)).EnsureSuccessStatusCode();
        return projectId;
    }

    private async Task RunWithArtifactsAsync(string testId, Func<IPage, Task> callback)
    {
        await using var context = await _browser.NewContextAsync(new() { BaseURL = _siteBaseUrl });
        var tracePath = Path.Combine(_artifactRoot, testId, "trace.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(tracePath)!);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();

        try
        {
            await callback(page);
        }
        catch
        {
            await page.ScreenshotAsync(new() { Path = Path.Combine(_artifactRoot, testId, "failure.png"), FullPage = true });
            throw;
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = tracePath });
        }
    }
}
