using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileTestRunStore : ITestRunStore
{
    private readonly AppConfig _cfg;
    private readonly ILogger<FileTestRunStore> _logger;

    public FileTestRunStore(AppConfig cfg, ILogger<FileTestRunStore> logger)
    {
        _cfg = cfg;
        _logger = logger;
    }

    private string RootPath => Path.Combine(_cfg.WorkingDirectory, "run-history");

    private string OwnerPath(string ownerKey)
        => Path.Combine(RootPath, Sanitize(ownerKey));

    private string ProjectPath(string ownerKey, string projectKey)
        => Path.Combine(OwnerPath(ownerKey), Sanitize(projectKey));

    private string RunFilePath(string ownerKey, string projectKey, Guid runId)
        => Path.Combine(ProjectPath(ownerKey, projectKey), $"{runId:D}.json");

    public async Task SaveAsync(TestRunRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        var ownerKey = string.IsNullOrWhiteSpace(record.OwnerKey) ? OwnerKeyDefaults.Default : record.OwnerKey.Trim();
        var projectKey = string.IsNullOrWhiteSpace(record.ProjectKey) ? "default" : record.ProjectKey.Trim();

        Directory.CreateDirectory(ProjectPath(ownerKey, projectKey));

        var path = RunFilePath(ownerKey, projectKey, record.RunId);
        _logger.LogInformation(
            "Saving run {RunId} for owner {OwnerKey} project {ProjectKey} to {Path}",
            record.RunId,
            ownerKey,
            projectKey,
            path);
        var json = JsonSerializer.Serialize(record, JsonDefaults.Default);

        await File.WriteAllTextAsync(path, json);
    }

    public async Task<TestRunRecord?> GetAsync(string ownerKey, Guid runId)
    {
        ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey.Trim();

        if (!Directory.Exists(RootPath))
            return null;

        var ownerDir = OwnerPath(ownerKey);
        if (!Directory.Exists(ownerDir))
            return null;

        // Search all projects for the runId file
        foreach (var projectDir in Directory.EnumerateDirectories(ownerDir))
        {
            var candidate = Path.Combine(projectDir, $"{runId:D}.json");
            if (!File.Exists(candidate))
                continue;

            var json = await File.ReadAllTextAsync(candidate);
            return JsonSerializer.Deserialize<TestRunRecord>(json, JsonDefaults.Default);
        }

        return null;
    }

    public async Task<bool> SetBaselineAsync(string ownerKey, Guid runId, Guid baselineRunId)
    {
        ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey.Trim();

        if (!Directory.Exists(RootPath))
            return false;

        var ownerDir = OwnerPath(ownerKey);
        if (!Directory.Exists(ownerDir))
            return false;

        var baseline = await GetAsync(ownerKey, baselineRunId);
        if (baseline is null)
            return false;

        foreach (var projectDir in Directory.EnumerateDirectories(ownerDir))
        {
            var candidate = Path.Combine(projectDir, $"{runId:D}.json");
            if (!File.Exists(candidate))
                continue;

            var json = await File.ReadAllTextAsync(candidate);
            var record = JsonSerializer.Deserialize<TestRunRecord>(json, JsonDefaults.Default);
            if (record is null)
                return false;

            record.BaselineRunId = baselineRunId;
            var updatedJson = JsonSerializer.Serialize(record, JsonDefaults.Default);
            await File.WriteAllTextAsync(candidate, updatedJson);
            return true;
        }

        return false;
    }

    public async Task<PagedResult<TestRunRecord>> ListAsync(
        string ownerKey,
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null)
    {
        ownerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey.Trim();
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();

        var dir = ProjectPath(ownerKey, projectKey);
        if (!Directory.Exists(dir))
            return new PagedResult<TestRunRecord>(Array.Empty<TestRunRecord>(), 0, null);

        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(f => new FileInfo(f))
            .ToList();

        var list = new List<TestRunRecord>(files.Count);

        foreach (var fi in files)
        {
            var json = await File.ReadAllTextAsync(fi.FullName);
            var record = JsonSerializer.Deserialize<TestRunRecord>(json, JsonDefaults.Default);
            if (record is null)
                continue;

            if (string.IsNullOrWhiteSpace(record.OwnerKey))
            {
                record = new TestRunRecord
                {
                    RunId = record.RunId,
                    OwnerKey = ownerKey,
                    ProjectKey = record.ProjectKey,
                    OperationId = record.OperationId,
                    BaselineRunId = record.BaselineRunId,
                    StartedUtc = record.StartedUtc,
                    CompletedUtc = record.CompletedUtc,
                    Result = record.Result
                };
            }

            if (operationId is not null &&
                !string.Equals(record.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                continue;

            // Ensure ProjectKey is always set even for older records
            if (string.IsNullOrWhiteSpace(record.ProjectKey))
            {
                record = new TestRunRecord
                {
                    RunId = record.RunId,
                    OwnerKey = ownerKey,
                    ProjectKey = projectKey,
                    OperationId = record.OperationId,
                    BaselineRunId = record.BaselineRunId,
                    StartedUtc = record.StartedUtc,
                    CompletedUtc = record.CompletedUtc,
                    Result = record.Result
                };
            }

            list.Add(record);
        }

        var ordered = sortField switch
        {
            SortField.CreatedUtc => direction == SortDirection.Asc
                ? list.OrderBy(r => r.StartedUtc)
                : list.OrderByDescending(r => r.StartedUtc),
            _ => direction == SortDirection.Asc
                ? list.OrderBy(r => r.StartedUtc)
                : list.OrderByDescending(r => r.StartedUtc)
        };

        var total = list.Count;
        var page = ordered
            .Skip(request.Offset)
            .Take(request.PageSize)
            .ToList();

        int? nextOffset = request.Offset + page.Count < total
            ? request.Offset + page.Count
            : null;

        return new PagedResult<TestRunRecord>(page, total, nextOffset);
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.Where(c => !invalid.Contains(c)).ToArray();
        var cleaned = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }
}
