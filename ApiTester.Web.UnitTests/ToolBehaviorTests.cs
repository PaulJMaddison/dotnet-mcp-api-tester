using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Runtime;
using ApiTester.McpServer.Services;
using ApiTester.McpServer.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class ProjectToolsTests
{
    [Fact]
    public async Task ApiCreateProject_RejectsEmptyName()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var tools = new ProjectTools(store, new ProjectContext(), NullLogger<ProjectTools>.Instance);

            var response = await tools.ApiCreateProject("   ", CancellationToken.None);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.True(doc.RootElement.GetProperty("isError").GetBoolean());
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task ApiListProjects_ClampsTake()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var tools = new ProjectTools(store, new ProjectContext(), NullLogger<ProjectTools>.Instance);

            var response = await tools.ApiListProjects(500, CancellationToken.None);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.Equal(200, doc.RootElement.GetProperty("pageSize").GetInt32());
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task ApiSetCurrentProject_ReturnsOkFalse_OnInvalidGuid()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var tools = new ProjectTools(store, new ProjectContext(), NullLogger<ProjectTools>.Instance);

            var response = await tools.ApiSetCurrentProject("not-a-guid", CancellationToken.None);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task ApiGetCurrentProject_ReturnsNull_WhenUnset()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var tools = new ProjectTools(store, new ProjectContext(), NullLogger<ProjectTools>.Instance);

            var response = await tools.ApiGetCurrentProject(CancellationToken.None);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("currentProjectId").ValueKind);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"apitester-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void DeleteTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }
}

public sealed class RunHistoryToolsTests
{
    [Fact]
    public async Task ApiListRuns_ClampsTake_AndUsesDefaultProjectKey()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileTestRunStore(new AppConfig { WorkingDirectory = tempDir }, NullLogger<FileTestRunStore>.Instance);
            var tools = new RunHistoryTools(store, NullLogger<RunHistoryTools>.Instance);

            await store.SaveAsync(BuildRun("default", "op-a"));

            var response = await tools.ApiListRuns(500, null, null);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.Equal("default", doc.RootElement.GetProperty("projectKey").GetString());
            Assert.Equal(200, doc.RootElement.GetProperty("pageSize").GetInt32());
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task ApiListRuns_FiltersByOperationId()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileTestRunStore(new AppConfig { WorkingDirectory = tempDir }, NullLogger<FileTestRunStore>.Instance);
            var tools = new RunHistoryTools(store, NullLogger<RunHistoryTools>.Instance);

            await store.SaveAsync(BuildRun("default", "op-a"));
            await store.SaveAsync(BuildRun("default", "op-b"));

            var response = await tools.ApiListRuns(20, "default", "op-a");

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
            var runs = doc.RootElement.GetProperty("runs").EnumerateArray().ToList();
            Assert.Single(runs);
            Assert.Equal("op-a", runs[0].GetProperty("operationId").GetString());
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task ApiGetRun_ReturnsNull_WhenMissing()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileTestRunStore(new AppConfig { WorkingDirectory = tempDir }, NullLogger<FileTestRunStore>.Instance);
            var tools = new RunHistoryTools(store, NullLogger<RunHistoryTools>.Instance);

            var response = await tools.ApiGetRun(Guid.NewGuid().ToString());

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.True(doc.RootElement.TryGetProperty("run", out var run));
            Assert.Equal(JsonValueKind.Null, run.ValueKind);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static TestRunRecord BuildRun(string projectKey, string operationId)
    {
        return new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            OwnerKey = OwnerKeyDefaults.Default,
            ProjectKey = projectKey,
            OperationId = operationId,
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = operationId,
                TotalCases = 1,
                Passed = 1,
                Failed = 0,
                Blocked = 0,
                TotalDurationMs = 5,
                Results = new List<TestCaseResult>
                {
                    new()
                    {
                        Name = "Happy",
                        Blocked = false,
                        Pass = true,
                        StatusCode = 200
                    }
                }
            }
        };
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"apitester-runs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void DeleteTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }
}

public sealed class PolicyToolsTests
{
    [Fact]
    public async Task ApiResetRuntime_ClearsBaseUrlAuthAndPolicy()
    {
        var runtime = new ApiRuntimeConfig();
        runtime.SetBaseUrl("https://example.com");
        runtime.SetBearerToken("token");
        runtime.Policy.DryRun = false;
        runtime.Policy.AllowedMethods.Add("POST");
        runtime.Policy.AllowedBaseUrls.Add("https://example.com");
        runtime.Policy.MaxResponseBodyBytes = 42;

        var tools = new PolicyTools(runtime, new NullAuditEventStore());
        var response = await tools.ApiResetRuntime();

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("baseUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("bearerToken").ValueKind);
        var policy = doc.RootElement.GetProperty("policy");
        Assert.True(policy.GetProperty("dryRun").GetBoolean());
        Assert.Equal(0, policy.GetProperty("allowedBaseUrls").GetArrayLength());
    }

    [Fact]
    public async Task ApiSetPolicy_ReturnsIsError_OnInvalidJson()
    {
        var tools = new PolicyTools(new ApiRuntimeConfig(), new NullAuditEventStore());

        var response = await tools.ApiSetPolicy("not-json");

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.True(doc.RootElement.GetProperty("isError").GetBoolean());
    }
}

public sealed class RuntimeToolsTests
{
    [Fact]
    public async Task ApiSetBaseUrl_RejectsInvalidScheme()
    {
        var runtime = new ApiRuntimeConfig();
        var tools = new RuntimeTools(runtime, new NullAuditEventStore());

        var response = await tools.ApiSetBaseUrl("ftp://example.com");

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.True(doc.RootElement.GetProperty("isError").GetBoolean());
        Assert.Null(runtime.BaseUrl);
    }
}

internal sealed class NullAuditEventStore : IAuditEventStore
{
    public Task<AuditEventRecord> CreateAsync(AuditEventRecord record, CancellationToken ct)
        => Task.FromResult(record);

    public Task<IReadOnlyList<AuditEventRecord>> ListAsync(
        Guid organisationId,
        int take,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AuditEventRecord>>(Array.Empty<AuditEventRecord>());
}
