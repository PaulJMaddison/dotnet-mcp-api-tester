using System.Diagnostics;
using System.Net;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.Web.Auth;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Ui.E2E;

public sealed class E2eFixture : IAsyncLifetime
{
    private const string SolutionName = "DotnetMcpApiTester.sln";
    private const string ProjectName = "E2E Project";
    private const string ProjectKeyValue = "e2e-project";
    private const string OwnerKeyValue = "e2e-user";
    private const string TargetFramework = "net8.0";

    private Process? _apiProcess;
    private Process? _uiProcess;
    private string _workingDirectory = "";
    private string _dbPath = "";

    public string ApiKey { get; private set; } = "";
    public Uri ApiBaseUri { get; private set; } = default!;
    public Uri UiBaseUri { get; private set; } = default!;
    public Guid ProjectId { get; private set; }
    public Guid RunId { get; private set; }
    public string ProjectKey => ProjectKeyValue;

    public async Task InitializeAsync()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), $"apitester-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workingDirectory);
        _dbPath = Path.Combine(_workingDirectory, "apitester.db");

        var token = ApiKeyToken.Generate();
        ApiKey = token.Token;
        ProjectId = Guid.NewGuid();
        RunId = Guid.NewGuid();

        SeedDatabase(token.Prefix);

        var apiPort = PortAllocator.GetFreePort();
        var uiPort = PortAllocator.GetFreePort();

        ApiBaseUri = new Uri($"http://127.0.0.1:{apiPort}");
        UiBaseUri = new Uri($"http://127.0.0.1:{uiPort}");

        var apiDllPath = ResolveAppDllPath("ApiTester.Web", "ApiTester.Web.dll");
        var uiDllPath = ResolveAppDllPath("ApiTester.Ui", "ApiTester.Ui.dll");

        _apiProcess = StartProcess(
            "dotnet",
            apiDllPath,
            new Dictionary<string, string?>
            {
                ["ASPNETCORE_URLS"] = ApiBaseUri.ToString(),
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["DOTNET_ENVIRONMENT"] = "Testing",
                ["Persistence__Provider"] = "Sqlite",
                ["Persistence__ConnectionString"] = $"Data Source={_dbPath}",
                ["Execution__AllowedBaseUrls__0"] = "https://httpbin.org"
            });

        await WaitForHealthyAsync(new Uri(ApiBaseUri, "/health"), TimeSpan.FromSeconds(60));

        _uiProcess = StartProcess(
            "dotnet",
            uiDllPath,
            new Dictionary<string, string?>
            {
                ["ASPNETCORE_URLS"] = UiBaseUri.ToString(),
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["DOTNET_ENVIRONMENT"] = "Testing",
                ["Auth__ApiKey"] = ApiKey,
                ["ApiTesterWeb__BaseUrl"] = ApiBaseUri.ToString()
            });

        await WaitForHealthyAsync(new Uri(UiBaseUri, "/ping"), TimeSpan.FromSeconds(60));
    }

    public async Task DisposeAsync()
    {
        await StopProcessAsync(_uiProcess);
        await StopProcessAsync(_apiProcess);

        try
        {
            await WaitForFileReleaseAsync(_dbPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        await DeleteWorkingDirectoryAsync();
    }

    private void SeedDatabase(string apiKeyPrefix)
    {
        var options = new DbContextOptionsBuilder<ApiTesterDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        using var db = new ApiTesterDbContext(options);
        db.Database.EnsureCreated();

        var orgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var now = DateTime.UtcNow;

        db.Organisations.Add(new OrganisationEntity
        {
            OrganisationId = orgId,
            Name = "E2E Org",
            Slug = "e2e-org",
            CreatedUtc = now
        });

        db.Users.Add(new UserEntity
        {
            UserId = userId,
            ExternalId = OwnerKeyValue,
            DisplayName = "E2E User",
            Email = "e2e@example.test",
            CreatedUtc = now
        });

        db.Memberships.Add(new MembershipEntity
        {
            OrganisationId = orgId,
            UserId = userId,
            Role = OrgRole.Owner,
            CreatedUtc = now
        });

        db.ApiKeys.Add(new ApiKeyEntity
        {
            KeyId = Guid.NewGuid(),
            OrganisationId = orgId,
            UserId = userId,
            Name = "E2E Key",
            Scopes = ApiKeyScopes.Serialize(new[]
            {
                ApiKeyScopes.ProjectsRead,
                ApiKeyScopes.ProjectsWrite,
                ApiKeyScopes.RunsRead,
                ApiKeyScopes.RunsWrite
            }),
            ExpiresUtc = null,
            RevokedUtc = null,
            Hash = ApiKeyHasher.Hash(ApiKey),
            Prefix = apiKeyPrefix
        });

        db.Projects.Add(new ProjectEntity
        {
            ProjectId = ProjectId,
            OrganisationId = orgId,
            TenantId = orgId,
            OwnerKey = OwnerKeyValue,
            Name = ProjectName,
            ProjectKey = ProjectKeyValue,
            CreatedUtc = now
        });

        db.TestRuns.Add(new TestRunEntity
        {
            RunId = RunId,
            OrganisationId = orgId,
            TenantId = orgId,
            ProjectId = ProjectId,
            OperationId = "op-e2e",
            StartedUtc = now.AddMinutes(-5),
            CompletedUtc = now.AddMinutes(-3),
            TotalCases = 1,
            Passed = 1,
            Failed = 0,
            Blocked = 0,
            TotalDurationMs = 250,
            Results =
            [
                new TestCaseResultEntity
                {
                    Name = "E2E check",
                    Method = "GET",
                    Url = "https://example.test",
                    StatusCode = 200,
                    DurationMs = 250,
                    Pass = true
                }
            ]
        });

        db.SaveChanges();
    }

    private static Process StartProcess(string fileName, string arguments, IDictionary<string, string?> environmentVariables)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = GetRepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var (key, value) in environmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, __) => { };
        process.ErrorDataReceived += (_, __) => { };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task StopProcessAsync(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task WaitForHealthyAsync(Uri uri, TimeSpan timeout)
    {
        using var client = new HttpClient();
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var response = await client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for {uri} to become healthy.");
    }

    private async Task DeleteWorkingDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_workingDirectory) || !Directory.Exists(_workingDirectory))
        {
            return;
        }

        const int maxAttempts = 10;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(_workingDirectory, true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(250);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(250);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private static async Task<bool> WaitForFileReleaseAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return true;
        }

        const int maxAttempts = 40;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(250);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(250);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    private static string ResolveAppDllPath(string projectName, string dllName)
    {
        var repoRoot = GetRepoRoot();
        var basePath = Path.Combine(repoRoot, projectName, "bin");
        var configurations = new[] { "Release", "Debug" };

        foreach (var configuration in configurations)
        {
            var candidate = Path.Combine(basePath, configuration, TargetFramework, dllName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var fallback = Path.Combine(basePath, "Debug", TargetFramework, dllName);
        throw new FileNotFoundException(
            $"Unable to locate {dllName}. Ensure the project has been built.",
            fallback);
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, SolutionName);
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate {SolutionName} from {AppContext.BaseDirectory}.");
    }

    private static class PortAllocator
    {
        public static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
