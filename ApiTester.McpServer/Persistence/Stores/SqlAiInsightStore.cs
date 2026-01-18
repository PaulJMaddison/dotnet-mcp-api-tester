using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlAiInsightStore : IAiInsightStore
{
    private readonly ApiTesterDbContext _db;

    public SqlAiInsightStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AiInsightRecord>> ListAsync(Guid organisationId, Guid projectId, Guid runId, CancellationToken ct)
    {
        return await _db.AiInsights.AsNoTracking()
            .Where(i => i.OrganisationId == organisationId && i.ProjectId == projectId && i.RunId == runId)
            .OrderBy(i => i.CreatedUtc)
            .Select(i => new AiInsightRecord(
                i.InsightId,
                i.OrganisationId,
                i.ProjectId,
                i.RunId,
                i.OperationId,
                i.Type,
                i.JsonPayload,
                i.ModelId,
                i.CreatedUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AiInsightRecord>> CreateAsync(
        Guid organisationId,
        Guid projectId,
        Guid runId,
        string operationId,
        IReadOnlyList<AiInsightCreate> insights,
        string modelId,
        DateTime createdUtc,
        CancellationToken ct)
    {
        if (insights is null || insights.Count == 0)
            return Array.Empty<AiInsightRecord>();

        operationId = (operationId ?? string.Empty).Trim();
        modelId = (modelId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation id is required.", nameof(operationId));

        var entities = insights
            .Select(insight => new AiInsightEntity
            {
                InsightId = Guid.NewGuid(),
                OrganisationId = organisationId,
                ProjectId = projectId,
                RunId = runId,
                OperationId = operationId,
                Type = NormalizeType(insight.Type),
                JsonPayload = NormalizePayload(insight.JsonPayload),
                ModelId = modelId,
                CreatedUtc = createdUtc
            })
            .ToList();

        _db.AiInsights.AddRange(entities);
        await _db.SaveChangesAsync(ct);

        return entities.Select(entity => new AiInsightRecord(
            entity.InsightId,
            entity.OrganisationId,
            entity.ProjectId,
            entity.RunId,
            entity.OperationId,
            entity.Type,
            entity.JsonPayload,
            entity.ModelId,
            entity.CreatedUtc)).ToList();
    }

    private static string NormalizeType(string? type)
    {
        type = (type ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Insight type is required.", nameof(type));
        return type;
    }

    private static string NormalizePayload(string? payload)
    {
        payload = (payload ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Insight payload is required.", nameof(payload));
        return payload;
    }
}
