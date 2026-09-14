using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ApiTester.McpServer.Models;
using ApiTester.Rag.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Rag;

/// <summary>
/// Converts an OpenAPI document into semantic evidence units for retrieval.
/// Operations, component schemas and security schemes remain intact instead of
/// being split at arbitrary character offsets.
/// </summary>
public sealed class OpenApiEvidenceBuilder
{
    public IReadOnlyList<RagChunk> Build(OpenApiSpecRecord spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.ProjectId == Guid.Empty)
            throw new ArgumentException("Specification projectId is required.", nameof(spec));
        if (spec.SpecId == Guid.Empty)
            throw new ArgumentException("Specification specId is required.", nameof(spec));
        if (string.IsNullOrWhiteSpace(spec.SpecJson))
            return Array.Empty<RagChunk>();

        var reader = new OpenApiStringReader();
        var document = reader.Read(spec.SpecJson, out _);
        if (document is null)
            return Array.Empty<RagChunk>();

        var chunks = new List<RagChunk>();
        AddOperationChunks(document, spec, chunks);
        AddSchemaChunks(document, spec, chunks);
        AddSecurityChunks(document, spec, chunks);
        return chunks;
    }

    private static void AddOperationChunks(OpenApiDocument document, OpenApiSpecRecord spec, List<RagChunk> chunks)
    {
        var ordinal = 0;
        foreach (var path in document.Paths.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (var operationEntry in path.Value.Operations.OrderBy(o => o.Key.ToString(), StringComparer.Ordinal))
            {
                var method = operationEntry.Key.ToString().ToUpperInvariant();
                var operation = operationEntry.Value;
                var operationId = string.IsNullOrWhiteSpace(operation.OperationId)
                    ? $"{method.ToLowerInvariant()}_{Sanitize(path.Key)}"
                    : operation.OperationId.Trim();

                var text = BuildOperationText(document, path.Key, path.Value, method, operationId, operation, spec);
                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EvidenceType"] = "operation",
                    ["Title"] = spec.Title,
                    ["Version"] = spec.Version,
                    ["Method"] = method,
                    ["Path"] = path.Key,
                    ["OperationId"] = operationId
                };

                chunks.Add(CreateChunk(
                    spec,
                    $"openapi:{spec.SpecId:N}:operation:{ordinal:D4}",
                    text,
                    metadata));
                ordinal++;
            }
        }
    }

    private static string BuildOperationText(
        OpenApiDocument document,
        string path,
        OpenApiPathItem pathItem,
        string method,
        string operationId,
        OpenApiOperation operation,
        OpenApiSpecRecord spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("EVIDENCE TYPE: OpenAPI operation");
        sb.AppendLine($"API: {spec.Title} {spec.Version}".TrimEnd());
        sb.AppendLine($"METHOD: {method}");
        sb.AppendLine($"PATH: {path}");
        sb.AppendLine($"OPERATION ID: {operationId}");

        if (!string.IsNullOrWhiteSpace(operation.Summary))
            sb.AppendLine($"SUMMARY: {operation.Summary.Trim()}");
        if (!string.IsNullOrWhiteSpace(operation.Description))
            sb.AppendLine($"DESCRIPTION: {operation.Description.Trim()}");

        var parameters = MergeParameters(pathItem.Parameters, operation.Parameters);
        if (parameters.Count > 0)
        {
            sb.AppendLine("PARAMETERS:");
            foreach (var parameter in parameters)
            {
                sb.Append("- ")
                    .Append(parameter.Name)
                    .Append(" in=")
                    .Append(parameter.In)
                    .Append(" required=")
                    .Append(parameter.Required ? "true" : "false")
                    .Append(" schema=")
                    .AppendLine(SummarizeSchema(parameter.Schema));

                if (!string.IsNullOrWhiteSpace(parameter.Description))
                    sb.AppendLine($"  description: {parameter.Description.Trim()}");
            }
        }
        else
        {
            sb.AppendLine("PARAMETERS: none documented");
        }

        if (operation.RequestBody is not null)
        {
            sb.AppendLine($"REQUEST BODY: required={operation.RequestBody.Required.ToString().ToLowerInvariant()}");
            foreach (var content in operation.RequestBody.Content.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"- content-type={content.Key} schema={SummarizeSchema(content.Value.Schema)}");
        }
        else
        {
            sb.AppendLine("REQUEST BODY: none documented");
        }

        var security = operation.Security is { Count: > 0 }
            ? operation.Security
            : document.SecurityRequirements;
        sb.AppendLine($"SECURITY: {SummarizeSecurityRequirements(security)}");

        sb.AppendLine("RESPONSES:");
        if (operation.Responses.Count == 0)
        {
            sb.AppendLine("- none documented");
        }
        else
        {
            foreach (var response in operation.Responses.OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                sb.Append("- ").Append(response.Key);
                if (!string.IsNullOrWhiteSpace(response.Value.Description))
                    sb.Append(" ").Append(response.Value.Description.Trim());
                sb.AppendLine();

                foreach (var content in response.Value.Content.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"  content-type={content.Key} schema={SummarizeSchema(content.Value.Schema)}");
            }
        }

        return sb.ToString().Trim();
    }

    private static void AddSchemaChunks(OpenApiDocument document, OpenApiSpecRecord spec, List<RagChunk> chunks)
    {
        if (document.Components?.Schemas is null)
            return;

        var ordinal = 0;
        foreach (var schemaEntry in document.Components.Schemas.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var sb = new StringBuilder();
            sb.AppendLine("EVIDENCE TYPE: OpenAPI component schema");
            sb.AppendLine($"API: {spec.Title} {spec.Version}".TrimEnd());
            sb.AppendLine($"SCHEMA: {schemaEntry.Key}");
            sb.AppendLine($"DEFINITION: {SummarizeSchema(schemaEntry.Value, includeProperties: true)}");

            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EvidenceType"] = "schema",
                ["Title"] = spec.Title,
                ["Version"] = spec.Version,
                ["SchemaName"] = schemaEntry.Key
            };

            chunks.Add(CreateChunk(
                spec,
                $"openapi:{spec.SpecId:N}:schema:{ordinal:D4}",
                sb.ToString().Trim(),
                metadata));
            ordinal++;
        }
    }

    private static void AddSecurityChunks(OpenApiDocument document, OpenApiSpecRecord spec, List<RagChunk> chunks)
    {
        if (document.Components?.SecuritySchemes is null)
            return;

        var ordinal = 0;
        foreach (var entry in document.Components.SecuritySchemes.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var scheme = entry.Value;
            var sb = new StringBuilder();
            sb.AppendLine("EVIDENCE TYPE: OpenAPI security scheme");
            sb.AppendLine($"API: {spec.Title} {spec.Version}".TrimEnd());
            sb.AppendLine($"SECURITY SCHEME: {entry.Key}");
            sb.AppendLine($"TYPE: {scheme.Type}");
            if (!string.IsNullOrWhiteSpace(scheme.Scheme)) sb.AppendLine($"SCHEME: {scheme.Scheme}");
            if (!string.IsNullOrWhiteSpace(scheme.BearerFormat)) sb.AppendLine($"BEARER FORMAT: {scheme.BearerFormat}");
            if (!string.IsNullOrWhiteSpace(scheme.Name)) sb.AppendLine($"NAME: {scheme.Name}");
            sb.AppendLine($"IN: {scheme.In}");
            if (!string.IsNullOrWhiteSpace(scheme.Description)) sb.AppendLine($"DESCRIPTION: {scheme.Description.Trim()}");

            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EvidenceType"] = "security",
                ["Title"] = spec.Title,
                ["Version"] = spec.Version,
                ["SecurityScheme"] = entry.Key
            };

            chunks.Add(CreateChunk(
                spec,
                $"openapi:{spec.SpecId:N}:security:{ordinal:D4}",
                sb.ToString().Trim(),
                metadata));
            ordinal++;
        }
    }

    private static IReadOnlyList<OpenApiParameter> MergeParameters(
        IList<OpenApiParameter>? pathParameters,
        IList<OpenApiParameter>? operationParameters)
    {
        var merged = new Dictionary<string, OpenApiParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in pathParameters ?? Array.Empty<OpenApiParameter>())
            merged[$"{parameter.In}:{parameter.Name}"] = parameter;
        foreach (var parameter in operationParameters ?? Array.Empty<OpenApiParameter>())
            merged[$"{parameter.In}:{parameter.Name}"] = parameter;

        return merged.Values
            .OrderBy(p => p.In.ToString(), StringComparer.Ordinal)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string SummarizeSecurityRequirements(IList<OpenApiSecurityRequirement>? requirements)
    {
        if (requirements is null || requirements.Count == 0)
            return "none documented";

        var groups = requirements.Select(requirement =>
            string.Join(" + ", requirement.Select(entry =>
            {
                var name = entry.Key.Reference?.Id;
                if (string.IsNullOrWhiteSpace(name))
                    name = !string.IsNullOrWhiteSpace(entry.Key.Scheme) ? entry.Key.Scheme : entry.Key.Type.ToString();
                var scopes = entry.Value.Count == 0 ? string.Empty : $" scopes=[{string.Join(",", entry.Value)}]";
                return name + scopes;
            })));

        return string.Join(" OR ", groups);
    }

    private static string SummarizeSchema(OpenApiSchema? schema, bool includeProperties = false, int depth = 0)
    {
        if (schema is null)
            return "unspecified";
        if (depth > 2)
            return "...";
        if (!string.IsNullOrWhiteSpace(schema.Reference?.Id))
            return $"ref={schema.Reference.Id}";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(schema.Type)) parts.Add($"type={schema.Type}");
        if (!string.IsNullOrWhiteSpace(schema.Format)) parts.Add($"format={schema.Format}");
        if (schema.Nullable) parts.Add("nullable=true");
        if (schema.Minimum is not null) parts.Add($"minimum={schema.Minimum.Value.ToString(CultureInfo.InvariantCulture)}");
        if (schema.Maximum is not null) parts.Add($"maximum={schema.Maximum.Value.ToString(CultureInfo.InvariantCulture)}");
        if (schema.MinLength is not null) parts.Add($"minLength={schema.MinLength.Value}");
        if (schema.MaxLength is not null) parts.Add($"maxLength={schema.MaxLength.Value}");
        if (schema.MinItems is not null) parts.Add($"minItems={schema.MinItems.Value}");
        if (schema.MaxItems is not null) parts.Add($"maxItems={schema.MaxItems.Value}");
        if (!string.IsNullOrWhiteSpace(schema.Pattern)) parts.Add($"pattern={schema.Pattern}");
        if (schema.Enum is { Count: > 0 }) parts.Add($"enum=[{string.Join(",", schema.Enum.Select(FormatOpenApiAny))}]");
        if (schema.Required is { Count: > 0 }) parts.Add($"required=[{string.Join(",", schema.Required)}]");

        if (schema.Items is not null)
            parts.Add($"items=({SummarizeSchema(schema.Items, includeProperties, depth + 1)})");

        if (includeProperties && schema.Properties is { Count: > 0 })
        {
            var properties = schema.Properties.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}({SummarizeSchema(p.Value, includeProperties: false, depth + 1)})");
            parts.Add($"properties=[{string.Join("; ", properties)}]");
        }

        return parts.Count == 0 ? "unspecified" : string.Join(" ", parts);
    }

    private static string FormatOpenApiAny(IOpenApiAny value) => value switch
    {
        OpenApiString s => s.Value,
        OpenApiInteger i => i.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiLong l => l.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiFloat f => f.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiDouble d => d.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiBoolean b => b.Value ? "true" : "false",
        _ => value.ToString() ?? string.Empty
    };

    private static RagChunk CreateChunk(
        OpenApiSpecRecord spec,
        string chunkId,
        string text,
        IReadOnlyDictionary<string, string> metadata) =>
        new(
            spec.ProjectId,
            "openapi",
            spec.SpecId.ToString(),
            chunkId,
            text,
            ComputeSha256Hex(text),
            spec.CreatedUtc,
            metadata);

    private static string ComputeSha256Hex(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        return sb.ToString().Trim('_');
    }
}
