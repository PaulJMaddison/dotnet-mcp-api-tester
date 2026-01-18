using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileGeneratedDocsStore : IGeneratedDocsStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileGeneratedDocsStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "generated-docs.json");

    public async Task<GeneratedDocsRecord?> GetAsync(Guid organisationId, Guid projectId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(record => record.OrganisationId == organisationId && record.ProjectId == projectId);
    }

    public async Task<GeneratedDocsRecord> UpsertAsync(
        Guid organisationId,
        Guid projectId,
        Guid specId,
        string docsJson,
        DateTime generatedUtc,
        CancellationToken ct)
    {
        docsJson = (docsJson ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(docsJson))
            throw new ArgumentException("Generated docs payload is required.", nameof(docsJson));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existingIndex = list.FindIndex(record => record.OrganisationId == organisationId && record.ProjectId == projectId);
            if (existingIndex >= 0)
            {
                var existing = list[existingIndex];
                var updated = existing with
                {
                    SpecId = specId,
                    DocsJson = docsJson,
                    UpdatedUtc = generatedUtc
                };
                list[existingIndex] = updated;
                await SaveAsync(list, ct);
                return updated;
            }

            var record = new GeneratedDocsRecord(
                Guid.NewGuid(),
                organisationId,
                projectId,
                specId,
                docsJson,
                generatedUtc,
                generatedUtc);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<GeneratedDocsRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<GeneratedDocsRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<GeneratedDocsRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
