using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace ApiTester.Web.AI;

public sealed class AiExplainService
{
    private readonly IAiProvider _provider;
    private readonly RedactionService _redactionService;
    private readonly IAiInsightStore _insightStore;
    private readonly TimeProvider _timeProvider;

    public AiExplainService(
        IAiProvider provider,
        RedactionService redactionService,
        IAiInsightStore insightStore,
        TimeProvider timeProvider)
    {
        _provider = provider;
        _redactionService = redactionService;
        _insightStore = insightStore;
        _timeProvider = timeProvider;
    }

    public async Task<AiExplainResult> ExplainAsync(AiExplainInput input, CancellationToken ct)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var org = input.Organisation ?? throw new ArgumentNullException(nameof(input.Organisation));
        var prompt = BuildPrompt(input, org.RedactionRules);
        var response = await _provider.CompleteAsync(prompt, ct);
        var redactedContent = _redactionService.RedactText(response.Content, org.RedactionRules) ?? response.Content;

        var payload = AiExplainSchemas.ParseExplain(redactedContent);
        var createdUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _insightStore.CreateAsync(
            org.OrganisationId,
            input.ProjectId,
            Guid.Empty,
            input.OperationId,
            new[] { new AiInsightCreate("explain", redactedContent) },
            response.ModelId,
            createdUtc,
            ct);

        return new AiExplainResult(payload, redactedContent, response.ModelId, createdUtc);
    }

    private AiRequest BuildPrompt(AiExplainInput input, IReadOnlyList<string>? redactionRules)
    {
        var examples = ExtractExamples(input.Operation, redactionRules);
        var context = new AiExplainContext(
            input.OperationId,
            input.HttpMethod,
            input.Path,
            input.Operation.Summary,
            input.Operation.Description,
            input.Operation.Tags?.Select(tag => tag.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [],
            BuildParameters(input.Operation),
            BuildRequestBody(input.Operation),
            BuildResponses(input.Operation),
            BuildAuthSchemes(input.Document, input.Operation),
            examples);

        var contextJson = JsonSerializer.Serialize(context, JsonDefaults.Default);
        contextJson = _redactionService.RedactText(contextJson, redactionRules) ?? contextJson;

        var systemPrompt = """
You are an API documentation assistant. Use only the provided context.
Return JSON that matches the required schema exactly.
Include a markdown field that can be rendered as documentation.
""";

        var userPrompt = new StringBuilder()
            .AppendLine("Schema:")
            .AppendLine(AiExplainSchemas.SchemaJson)
            .AppendLine()
            .AppendLine("Context:")
            .AppendLine(contextJson)
            .ToString();

        return new AiRequest(systemPrompt, userPrompt);
    }

    private static IReadOnlyList<AiExplainParameter> BuildParameters(OpenApiOperation operation)
        => operation.Parameters?
            .Select(p => new AiExplainParameter(
                p.Name,
                p.In?.ToString() ?? string.Empty,
                p.Required,
                p.Schema?.Type,
                p.Schema?.Format,
                p.Description))
            .ToList() ?? [];

    private static AiExplainRequestBody? BuildRequestBody(OpenApiOperation operation)
    {
        var body = operation.RequestBody;
        if (body is null)
            return null;

        var content = body.Content?
            .Select(entry => new AiExplainContent(
                entry.Key,
                entry.Value.Schema?.Type,
                entry.Value.Schema?.Format))
            .ToList() ?? [];

        return new AiExplainRequestBody(body.Description, body.Required, content);
    }

    private static IReadOnlyList<AiExplainResponseContext> BuildResponses(OpenApiOperation operation)
        => operation.Responses?
            .Select(response => new AiExplainResponseContext(
                response.Key,
                response.Value.Description,
                response.Value.Content?.Select(c => c.Key).ToList() ?? []))
            .ToList() ?? [];

    private static IReadOnlyList<AiExplainAuthScheme> BuildAuthSchemes(OpenApiDocument document, OpenApiOperation operation)
    {
        var schemes = document.Components?.SecuritySchemes ?? new Dictionary<string, OpenApiSecurityScheme>();
        var requirements = operation.Security?.Count > 0
            ? operation.Security
            : document.Security ?? new List<OpenApiSecurityRequirement>();

        var results = new List<AiExplainAuthScheme>();
        foreach (var requirement in requirements)
        {
            foreach (var scheme in requirement.Keys)
            {
                var key = scheme.Reference?.Id ?? scheme.Scheme ?? scheme.Name ?? "unknown";
                if (!schemes.TryGetValue(key, out var resolved))
                    resolved = scheme;

                results.Add(new AiExplainAuthScheme(
                    key,
                    resolved.Type.ToString(),
                    resolved.In?.ToString(),
                    resolved.Scheme,
                    resolved.BearerFormat));
            }
        }

        return results;
    }

    private IReadOnlyList<AiExplainExampleContext> ExtractExamples(OpenApiOperation operation, IReadOnlyList<string>? redactionRules)
    {
        var list = new List<AiExplainExampleContext>();

        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters)
            {
                if (parameter.Example is not null)
                {
                    var rendered = RenderExample(parameter.Example);
                    if (!string.IsNullOrWhiteSpace(rendered))
                        list.Add(new AiExplainExampleContext(
                            $"parameter:{parameter.Name}",
                            parameter.Description,
                            null,
                            RedactExample(rendered, redactionRules)));
                }

                if (parameter.Examples is not null)
                {
                    foreach (var example in parameter.Examples)
                    {
                        var rendered = RenderExample(example.Value?.Value);
                        if (!string.IsNullOrWhiteSpace(rendered))
                            list.Add(new AiExplainExampleContext(
                                $"parameter:{parameter.Name}:{example.Key}",
                                example.Value?.Summary,
                                null,
                                RedactExample(rendered, redactionRules)));
                    }
                }
            }
        }

        if (operation.RequestBody is not null)
        {
            foreach (var content in operation.RequestBody.Content)
            {
                var examples = ExtractMediaExamples(content.Value);
                foreach (var example in examples)
                {
                    list.Add(new AiExplainExampleContext(
                        "requestBody",
                        example.Summary,
                        content.Key,
                        RedactExample(example.Value, redactionRules)));
                }
            }
        }

        if (operation.Responses is not null)
        {
            foreach (var response in operation.Responses)
            {
                foreach (var content in response.Value.Content ?? new Dictionary<string, OpenApiMediaType>())
                {
                    var examples = ExtractMediaExamples(content.Value);
                    foreach (var example in examples)
                    {
                        list.Add(new AiExplainExampleContext(
                            $"response:{response.Key}",
                            example.Summary,
                            content.Key,
                            RedactExample(example.Value, redactionRules)));
                    }
                }
            }
        }

        return list;
    }

    private string RedactExample(string example, IReadOnlyList<string>? redactionRules)
        => _redactionService.RedactText(example, redactionRules) ?? example;

    private static IReadOnlyList<AiExplainMediaExample> ExtractMediaExamples(OpenApiMediaType mediaType)
    {
        var list = new List<AiExplainMediaExample>();

        if (mediaType.Example is not null)
        {
            var rendered = RenderExample(mediaType.Example);
            if (!string.IsNullOrWhiteSpace(rendered))
                list.Add(new AiExplainMediaExample(null, rendered));
        }

        if (mediaType.Examples is not null)
        {
            foreach (var example in mediaType.Examples)
            {
                var rendered = RenderExample(example.Value?.Value) ?? example.Value?.ExternalValue;
                if (!string.IsNullOrWhiteSpace(rendered))
                    list.Add(new AiExplainMediaExample(example.Value?.Summary, rendered));
            }
        }

        return list;
    }

    private static string? RenderExample(IOpenApiAny? example)
    {
        if (example is null)
            return null;

        var writer = new OpenApiStringWriter();
        example.Write(writer, OpenApiSpecVersion.OpenApi3_0);
        return writer.ToString();
    }
}

public sealed record AiExplainInput(
    OrganisationRecord Organisation,
    Guid ProjectId,
    string OperationId,
    string HttpMethod,
    string Path,
    OpenApiDocument Document,
    OpenApiOperation Operation);

public sealed record AiExplainResult(
    AiExplainPayload Payload,
    string RawJson,
    string ModelId,
    DateTime CreatedUtc);

public sealed record AiExplainContext(
    string OperationId,
    string HttpMethod,
    string Path,
    string? Summary,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AiExplainParameter> Parameters,
    AiExplainRequestBody? RequestBody,
    IReadOnlyList<AiExplainResponseContext> Responses,
    IReadOnlyList<AiExplainAuthScheme> AuthSchemes,
    IReadOnlyList<AiExplainExampleContext> Examples);

public sealed record AiExplainParameter(
    string Name,
    string In,
    bool Required,
    string? SchemaType,
    string? SchemaFormat,
    string? Description);

public sealed record AiExplainRequestBody(
    string? Description,
    bool Required,
    IReadOnlyList<AiExplainContent> Content);

public sealed record AiExplainContent(
    string ContentType,
    string? SchemaType,
    string? SchemaFormat);

public sealed record AiExplainResponseContext(
    string StatusCode,
    string? Description,
    IReadOnlyList<string> ContentTypes);

public sealed record AiExplainAuthScheme(
    string Name,
    string Type,
    string? In,
    string? Scheme,
    string? BearerFormat);

public sealed record AiExplainExampleContext(
    string Source,
    string? Summary,
    string? ContentType,
    string Value);

public sealed record AiExplainMediaExample(string? Summary, string Value);
