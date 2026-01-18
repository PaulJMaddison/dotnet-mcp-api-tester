using System.ComponentModel;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Runtime;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class RagTools
{
    private readonly RagRuntime _rag;
    private readonly ProjectContext _ctx;
    private readonly IOpenApiSpecStore _specs;
    private readonly ILogger<RagTools> _logger;

    public RagTools(RagRuntime rag, ProjectContext ctx, IOpenApiSpecStore specs, ILogger<RagTools> logger)
    {
        _rag = rag;
        _ctx = ctx;
        _specs = specs;
        _logger = logger;
    }

    [McpServerTool, Description("Index the given project's OpenAPI specs into a local vector store for RAG. If projectId is omitted, uses the current project.")]
    public async Task<object> ApiRagIndexProject(string? projectId = null, CancellationToken ct = default)
    {
        // Deterministic, stateless behaviour for demos and automation
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            if (!Guid.TryParse(projectId, out var pid))
                return new { ok = false, reason = "Invalid projectId GUID." };

            _ctx.SetCurrentProject(pid);
        }

        var current = _ctx.CurrentProjectId;
        if (current is null)
            return new { ok = false, reason = "No current project. Pass projectId or call ApiSetCurrentProject or ApiCreateProject first." };

        var specs = await _specs.ListAsync(OrgDefaults.DefaultOrganisationId, current.Value, ct).ConfigureAwait(false);

        var indexedChunks = 0;

        foreach (var spec in specs)
        {
            ct.ThrowIfCancellationRequested();

            var chunks = _rag.Chunker.Chunk(
                projectId: spec.ProjectId,
                sourceType: "openapi",
                sourceId: spec.SpecId.ToString(),
                text: spec.SpecJson,
                metadata: new Dictionary<string, string>
                {
                    ["Title"] = spec.Title,
                    ["Version"] = spec.Version
                },
                createdUtc: spec.CreatedUtc);

            await _rag.Indexer.IndexAsync(chunks, ct).ConfigureAwait(false);
            indexedChunks += chunks.Count;

            _logger.LogInformation("Indexed {ChunkCount} chunks for spec {SpecId}", chunks.Count, spec.SpecId);
        }

        return new
        {
            ok = true,
            projectId = current.Value,
            specCount = specs.Count,
            indexedChunks
        };
    }

    [McpServerTool, Description("Ask a question about the given project using RAG over indexed OpenAPI specs. If projectId is omitted, uses the current project.")]
    public async Task<object> ApiRagAsk(string question, int topK = 10, string? projectId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new { ok = false, reason = "Question is required." };

        // Deterministic, stateless behaviour for demos and automation
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            if (!Guid.TryParse(projectId, out var pid))
                return new { ok = false, reason = "Invalid projectId GUID." };

            _ctx.SetCurrentProject(pid);
        }

        var current = _ctx.CurrentProjectId;
        if (current is null)
            return new { ok = false, reason = "No current project. Pass projectId or call ApiSetCurrentProject or ApiCreateProject first." };

        var result = await _rag.Answerer.AnswerAsync(current.Value, question, topK, ct).ConfigureAwait(false);

        return new
        {
            ok = true,
            projectId = current.Value,
            answer = result.Answer,
            evidence = result.Evidence.Select(e => new
            {
                chunkId = e.Chunk.ChunkId,
                sourceType = e.Chunk.SourceType,
                sourceId = e.Chunk.SourceId,
                score = e.Score,
                preview = e.Chunk.Text.Length <= 220 ? e.Chunk.Text : e.Chunk.Text[..220] + "…"
            }).ToList()
        };
    }
}
