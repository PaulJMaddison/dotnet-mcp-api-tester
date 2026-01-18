using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileTestRunStore : ITestRunStore
{
    private readonly AppConfig _cfg;
    private readonly ILogger<FileTestRunStore> _logger;
    private readonly IOrganisationStore _organisationStore;
    private readonly RedactionService _redactionService;

    public FileTestRunStore(
        AppConfig cfg,
        ILogger<FileTestRunStore> logger,
        IOrganisationStore organisationStore,
        RedactionService redactionService)
    {
        _cfg = cfg;
        _logger = logger;
        _organisationStore = organisationStore;
        _redactionService = redactionService;
    }

    private string RootPath => Path.Combine(_cfg.WorkingDirectory, "run-history");

    private string OrgPath(Guid organisationId)
        => Path.Combine(RootPath, Sanitize(NormalizeOrganisationId(organisationId).ToString("N")));

    private string ProjectPath(Guid organisationId, string projectKey)
        => Path.Combine(OrgPath(organisationId), Sanitize(projectKey));

    private string RunFilePath(Guid organisationId, string projectKey, Guid runId)
        => Path.Combine(ProjectPath(organisationId, projectKey), $"{runId:D}.json");

    public async Task SaveAsync(TestRunRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        if (record.Result is null) throw new ArgumentNullException(nameof(record.Result));
        var organisationId = NormalizeOrganisationId(record.OrganisationId);
        var ownerKey = string.IsNullOrWhiteSpace(record.OwnerKey) ? OwnerKeyDefaults.Default : record.OwnerKey.Trim();
        var projectKey = string.IsNullOrWhiteSpace(record.ProjectKey) ? "default" : record.ProjectKey.Trim();

        Directory.CreateDirectory(ProjectPath(organisationId, projectKey));

        var path = RunFilePath(organisationId, projectKey, record.RunId);
        _logger.LogInformation(
            "Saving run {RunId} for org {OrganisationId} owner {OwnerKey} project {ProjectKey} to {Path}",
            record.RunId,
            organisationId,
            ownerKey,
            projectKey,
            path);
        var org = await _organisationStore.GetAsync(organisationId, CancellationToken.None);
        var redactedResult = _redactionService.RedactResult(record.Result, org?.RedactionRules);
        redactedResult.ClassificationSummary = ResultClassificationRules.Summarize(redactedResult.Results);

        var recordToSave = new TestRunRecord
        {
            RunId = record.RunId,
            OrganisationId = organisationId,
            Actor = record.Actor,
            Environment = record.Environment,
            PolicySnapshot = record.PolicySnapshot,
            OwnerKey = ownerKey,
            ProjectKey = projectKey,
            OperationId = record.OperationId,
            SpecId = record.SpecId,
            BaselineRunId = record.BaselineRunId,
            StartedUtc = record.StartedUtc.ToUniversalTime(),
            CompletedUtc = record.CompletedUtc.ToUniversalTime(),
            Result = redactedResult
        };

        var json = JsonSerializer.Serialize(recordToSave, JsonDefaults.Default);

        await File.WriteAllTextAsync(path, json);
    }

    public async Task<TestRunRecord?> GetAsync(Guid organisationId, Guid runId)
    {
        organisationId = NormalizeOrganisationId(organisationId);

        if (!Directory.Exists(RootPath))
            return null;

        var orgDir = OrgPath(organisationId);
        if (!Directory.Exists(orgDir))
            return null;

        // Search all projects for the runId file
        foreach (var projectDir in Directory.EnumerateDirectories(orgDir))
        {
            var candidate = Path.Combine(projectDir, $"{runId:D}.json");
            if (!File.Exists(candidate))
                continue;

            var json = await File.ReadAllTextAsync(candidate);
            var record = JsonSerializer.Deserialize<TestRunRecord>(json, JsonDefaults.Default);
            if (record is not null)
            {
                record = NormalizeRecord(record, organisationId, Path.GetFileName(projectDir) ?? "default");
                record.Result.ClassificationSummary = ResultClassificationRules.Summarize(record.Result.Results);
            }
            return record;
        }

        return null;
    }

    public async Task<bool> SetBaselineAsync(Guid organisationId, Guid runId, Guid baselineRunId)
    {
        organisationId = NormalizeOrganisationId(organisationId);

        if (!Directory.Exists(RootPath))
            return false;

        var orgDir = OrgPath(organisationId);
        if (!Directory.Exists(orgDir))
            return false;

        var baseline = await GetAsync(organisationId, baselineRunId);
        if (baseline is null)
            return false;

        foreach (var projectDir in Directory.EnumerateDirectories(orgDir))
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
        Guid organisationId,
        string projectKey,
        PageRequest request,
        SortField sortField,
        SortDirection direction,
        string? operationId = null)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();

        var dir = ProjectPath(organisationId, projectKey);
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

            if (record.OrganisationId == Guid.Empty || string.IsNullOrWhiteSpace(record.OwnerKey))
            {
                record = new TestRunRecord
                {
                    RunId = record.RunId,
                    OrganisationId = organisationId,
                    Actor = record.Actor,
                    Environment = record.Environment,
                    PolicySnapshot = record.PolicySnapshot,
                    OwnerKey = OwnerKeyDefaults.Default,
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
                    OrganisationId = organisationId,
                    Actor = record.Actor,
                    Environment = record.Environment,
                    PolicySnapshot = record.PolicySnapshot,
                    OwnerKey = record.OwnerKey,
                    ProjectKey = projectKey,
                    OperationId = record.OperationId,
                    BaselineRunId = record.BaselineRunId,
                    StartedUtc = record.StartedUtc,
                    CompletedUtc = record.CompletedUtc,
                    Result = record.Result
                };
            }

            record.Result.ClassificationSummary = ResultClassificationRules.Summarize(record.Result.Results);

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

    public async Task<int> PruneAsync(Guid organisationId, DateTimeOffset cutoffUtc, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);

        var orgDir = OrgPath(organisationId);
        if (!Directory.Exists(orgDir))
            return 0;

        var deleted = 0;

        foreach (var projectDir in Directory.EnumerateDirectories(orgDir))
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                var json = await File.ReadAllTextAsync(file, ct);
                var record = JsonSerializer.Deserialize<TestRunRecord>(json, JsonDefaults.Default);
                if (record is null)
                    continue;

                var completed = record.CompletedUtc == default ? record.StartedUtc : record.CompletedUtc;
                if (completed < cutoffUtc)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
        }

        return deleted;
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.Where(c => !invalid.Contains(c)).ToArray();
        var cleaned = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }

    private static TestRunRecord NormalizeRecord(TestRunRecord record, Guid organisationId, string projectKey)
    {
        var normalizedOrganisationId = record.OrganisationId == Guid.Empty
            ? organisationId
            : record.OrganisationId;
        var normalizedOwnerKey = string.IsNullOrWhiteSpace(record.OwnerKey)
            ? OwnerKeyDefaults.Default
            : record.OwnerKey.Trim();
        var normalizedProjectKey = string.IsNullOrWhiteSpace(record.ProjectKey) ? projectKey : record.ProjectKey.Trim();
        if (normalizedOrganisationId != record.OrganisationId ||
            !string.Equals(normalizedOwnerKey, record.OwnerKey, StringComparison.Ordinal) ||
            !string.Equals(normalizedProjectKey, record.ProjectKey, StringComparison.Ordinal))
        {
            return new TestRunRecord
            {
                RunId = record.RunId,
                OrganisationId = normalizedOrganisationId,
                Actor = record.Actor,
                Environment = record.Environment,
                PolicySnapshot = record.PolicySnapshot,
                OwnerKey = normalizedOwnerKey,
                ProjectKey = string.IsNullOrWhiteSpace(normalizedProjectKey) ? "default" : normalizedProjectKey,
                OperationId = record.OperationId,
                SpecId = record.SpecId,
                BaselineRunId = record.BaselineRunId,
                StartedUtc = record.StartedUtc,
                CompletedUtc = record.CompletedUtc,
                Result = record.Result
            };
        }

        return record;
    }

    private static Guid NormalizeOrganisationId(Guid organisationId)
        => organisationId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : organisationId;
}
