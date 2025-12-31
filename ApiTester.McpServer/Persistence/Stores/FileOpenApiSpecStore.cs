using System.Text.Json;
using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileOpenApiSpecStore : IOpenApiSpecStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public FileOpenApiSpecStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "openapi-specs.json");

    public async Task<OpenApiSpecRecord?> GetAsync(Guid projectId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(s => s.ProjectId == projectId);
    }

    public async Task<OpenApiSpecRecord> UpsertAsync(Guid projectId, string title, string version, string specJson, DateTime createdUtc, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var index = list.FindIndex(s => s.ProjectId == projectId);
            var specId = index >= 0 ? list[index].SpecId : Guid.NewGuid();

            var record = new OpenApiSpecRecord(specId, projectId, title, version, specJson, createdUtc);
            if (index >= 0)
            {
                list[index] = record;
            }
            else
            {
                list.Add(record);
            }

            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<OpenApiSpecRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<OpenApiSpecRecord>>(json, JsonOptions) ?? [];
    }

    private async Task SaveAsync(List<OpenApiSpecRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonOptions);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
