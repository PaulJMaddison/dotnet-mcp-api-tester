using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;
using System.Text.Json;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileBaselineStore : IBaselineStore
{
    private readonly AppConfig _cfg;

    public FileBaselineStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string RootPath => Path.Combine(_cfg.WorkingDirectory, "run-history");

    private string OrgPath(Guid organisationId)
        => Path.Combine(RootPath, Sanitize(NormalizeOrganisationId(organisationId).ToString("N")));

    private string ProjectPath(Guid organisationId, string projectKey)
        => Path.Combine(OrgPath(organisationId), Sanitize(projectKey));

    private string BaselineFilePath(Guid organisationId, string projectKey)
        => Path.Combine(ProjectPath(organisationId, projectKey), "baselines.json");

    public async Task<BaselineRecord?> GetAsync(Guid organisationId, string projectKey, string operationId, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? string.Empty : operationId.Trim();

        var baseline = await FindBaselineAsync(organisationId, projectKey, operationId, ct);
        return baseline;
    }

    public async Task<IReadOnlyList<BaselineRecord>> ListAsync(Guid organisationId, string? projectKey, string? operationId, int take, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? null : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        take = take <= 0 ? 50 : Math.Min(take, 500);

        var baselines = new List<BaselineRecord>();

        if (!Directory.Exists(RootPath))
            return baselines;

        var orgDir = OrgPath(organisationId);
        if (!Directory.Exists(orgDir))
            return baselines;

        IEnumerable<string> projectDirs = projectKey is null
            ? Directory.EnumerateDirectories(orgDir)
            : new[] { ProjectPath(organisationId, projectKey) };

        foreach (var projectDir in projectDirs)
        {
            if (!Directory.Exists(projectDir))
                continue;

            var projectKeyName = Path.GetFileName(projectDir) ?? "default";
            var baselineFile = Path.Combine(projectDir, "baselines.json");
            if (!File.Exists(baselineFile))
                continue;

            var json = await File.ReadAllTextAsync(baselineFile, ct);
            var items = JsonSerializer.Deserialize<List<FileBaselineRecord>>(json, JsonDefaults.Default) ?? new List<FileBaselineRecord>();

            foreach (var item in items)
            {
                if (operationId is not null && !string.Equals(item.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                    continue;

                baselines.Add(new BaselineRecord(
                    item.RunId,
                    projectKeyName,
                    item.OperationId,
                    item.SetUtc));
            }
        }

        return baselines
            .OrderByDescending(x => x.SetUtc)
            .Take(take)
            .ToList();
    }

    public async Task<BaselineRecord?> SetAsync(Guid organisationId, string projectKey, string operationId, Guid runId, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? string.Empty : operationId.Trim();

        var projectDir = ProjectPath(organisationId, projectKey);
        Directory.CreateDirectory(projectDir);

        var baselineFile = BaselineFilePath(organisationId, projectKey);
        var items = await ReadBaselinesAsync(baselineFile, ct);

        var existing = items.FirstOrDefault(x => string.Equals(x.OperationId, operationId, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            items.Add(new FileBaselineRecord
            {
                OperationId = operationId,
                RunId = runId,
                SetUtc = now
            });
        }
        else
        {
            existing.RunId = runId;
            existing.SetUtc = now;
        }

        var json = JsonSerializer.Serialize(items, JsonDefaults.Default);
        await File.WriteAllTextAsync(baselineFile, json, ct);

        return new BaselineRecord(runId, projectKey, operationId, now);
    }

    private async Task<BaselineRecord?> FindBaselineAsync(Guid organisationId, string projectKey, string operationId, CancellationToken ct)
    {
        var baselineFile = BaselineFilePath(organisationId, projectKey);
        if (!File.Exists(baselineFile))
            return null;

        var items = await ReadBaselinesAsync(baselineFile, ct);
        var match = items.FirstOrDefault(x => string.Equals(x.OperationId, operationId, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? null
            : new BaselineRecord(match.RunId, projectKey, match.OperationId, match.SetUtc);
    }

    private static async Task<List<FileBaselineRecord>> ReadBaselinesAsync(string baselineFile, CancellationToken ct)
    {
        if (!File.Exists(baselineFile))
            return new List<FileBaselineRecord>();

        var json = await File.ReadAllTextAsync(baselineFile, ct);
        return JsonSerializer.Deserialize<List<FileBaselineRecord>>(json, JsonDefaults.Default) ?? new List<FileBaselineRecord>();
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }

    private static Guid NormalizeOrganisationId(Guid organisationId)
        => organisationId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : organisationId;

    private sealed class FileBaselineRecord
    {
        public string OperationId { get; set; } = "";
        public Guid RunId { get; set; }
        public DateTimeOffset SetUtc { get; set; }
    }
}
