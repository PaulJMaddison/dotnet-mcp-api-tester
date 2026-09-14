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
    private readonly OpenApiEvidenceBuilder _openApiEvidence;
    private readonly ILogger<RagTools> _logger;

    public RagTools(
        RagRuntime rag,
        ProjectContext ctx,
        IOpenApiSpecStore specs,
        OpenApiEvidenceBuilder openApiEvidence,
        ILogger<RagTools> logger)
    {
        _rag = rag ?? throw new ArgumentNullException(nameof(rag));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _specs = specs ?? throw new ArgumentNullException(nameof(specs));
        _openApiEvidence = openApiEvidence ?? throw new ArgumentNullException(nameof(openApiEvidence));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool, Description("Index the given project's OpenAPI specs into a vector store as operation/schema/security evidence for RAG. If projectId is omitted, uses the current project.")]
    public async Task<object> ApiRagIndexProject(string? projectId = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(projectId))
        {
            if (!Guid.TryParse(projectId, out var pid) || pid == Guid.Empty)
                return new { ok = false, reason = "Invalid projectId GUID." };

            _ctx.SetCurrentProject(pid);
        }

        var current = _ctx.CurrentProjectId;
        if (current is null)
            return new { ok = false, reason = "No current project. Pass projectId or call ApiSetCurrentProject or ApiCreateProject first." };

        var specs = await _specs.ListAsync(OrgDefaults.DefaultOrganisationId, current.Value, ct).ConfigureAwait(false);
        var indexedChunks = 0;
        var operationChunks = 0;
        var schemaChunks = 0;
        var securityChunks = 0;
        var genericFallbackChunks = 0;

        foreach (var spec in specs)
        {
            ct.ThrowIfCancellationRequested();

            if (spec.ProjectId != current.Value)
                throw new InvalidOperationException("OpenAPI store returned a specification for a different project context.");
            if (spec.TenantId != OrgDefaults.DefaultOrganisationId)
                throw new InvalidOperationException("OpenAPI store returned a specification for a different tenant context.");

            IReadOnlyList<ApiTester.Rag.Models.RagChunk> chunks = _openApiEvidence.Build(spec);
            if (chunks.Count == 0)
            {
                chunks = _rag.Chunker.Chunk(
                    projectId: current.Value,
                    sourceType: "openapi",
                    sourceId: spec.SpecId.ToString(),
                    text: spec.SpecJson,
                    metadata: new Dictionary<string, string>
                    {
                        ["Title"] = spec.Title,
                        ["Version"] = spec.Version,
                        ["EvidenceType"] = "generic-fallback"
                    },
                    createdUtc: spec.CreatedUtc);
                genericFallbackChunks += chunks.Count;
            }

            await _rag.Indexer.IndexAsync(chunks, ct).ConfigureAwait(false);
            indexedChunks += chunks.Count;
            operationChunks += chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("operation", StringComparison.OrdinalIgnoreCase));
            schemaChunks += chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("schema", StringComparison.OrdinalIgnoreCase));
            securityChunks += chunks.Count(c => c.Metadata.TryGetValue("EvidenceType", out var value) && value.Equals("security", StringComparison.OrdinalIgnoreCase));

            _logger.LogInformation(
                "Indexed {ChunkCount} structured OpenAPI evidence chunks for spec {SpecId}",
                chunks.Count,
                spec.SpecId);
        }

        return new
        {
            ok = true,
            projectId = current.Value,
            specCount = specs.Count,
            indexedChunks,
            evidence = new
            {
                operations = operationChunks,
                schemas = schemaChunks,
                securitySchemes = securityChunks,
                genericFallback = genericFallbackChunks
            }
        };
    }

    [McpServerTool, Description("Ask a question about the given project using RAG over indexed OpenAPI evidence. If projectId is omitted, uses the current project.")]
    public async Task<object> ApiRagAsk(string question, int topK = 10, string? projectId = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(question))
            return new { ok = false, reason = "Question is required." };

        if (!string.IsNullOrWhiteSpace(projectId))
        {
            if (!Guid.TryParse(projectId, out var pid) || pid == Guid.Empty)
                return new { ok = false, reason = "Invalid projectId GUID." };

            _ctx.SetCurrentProject(pid);
        }

        var current = _ctx.CurrentProjectId;
        if (current is null)
            return new { ok = false, reason = "No current project. Pass projectId or call ApiSetCurrentProject or ApiCreateProject first." };

        var result = await _rag.Answerer.AnswerAsync(current.Value, question.Trim(), topK, ct).ConfigureAwait(false);

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
                evidenceType = e.Chunk.Metadata.TryGetValue("EvidenceType", out var evidenceType) ? evidenceType : "unknown",
                operationId = e.Chunk.Metadata.TryGetValue("OperationId", out var operationId) ? operationId : null,
                schemaName = e.Chunk.Metadata.TryGetValue("SchemaName", out var schemaName) ? schemaName : null,
                score = e.Score,
                preview = e.Chunk.Text.Length <= 300 ? e.Chunk.Text : e.Chunk.Text[..300] + "…"
            }).ToList()
        };
    }
}
