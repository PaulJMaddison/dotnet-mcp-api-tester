using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class ApiAssistTools
{
    private readonly OpenApiStore _store;

    public ApiAssistTools(OpenApiStore store)
    {
        _store = store;
    }

    [McpServerTool, Description("Generate a deterministic test plan (summary + parameters + edge cases) for an operationId.")]
    public object ApiGenerateTestPlan(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        var doc = _store.RequireDocument();

        // Find operation by operationId
        (string path, string method, Microsoft.OpenApi.Models.OpenApiOperation op)? found = null;

        foreach (var p in doc.Paths)
        {
            foreach (var kv in p.Value.Operations)
            {
                var m = kv.Key.ToString().ToUpperInvariant();
                var o = kv.Value;

                if (string.Equals(o.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                {
                    found = (p.Key, m, o);
                    break;
                }
            }
            if (found is not null) break;
        }

        if (found is null)
            throw new InvalidOperationException($"operationId not found: {operationId}");

        var (pathKey, httpMethod, operation) = found.Value;

        var parameters = (operation.Parameters ?? new List<Microsoft.OpenApi.Models.OpenApiParameter>())
            .Select(p => new
            {
                name = p.Name,
                @in = p.In.ToString(),
                required = p.Required,
                description = p.Description ?? "",
                schema = new
                {
                    type = p.Schema?.Type ?? "",
                    format = p.Schema?.Format ?? "",
                    nullable = p.Schema?.Nullable ?? false
                }
            })
            .ToList();

        var responses = operation.Responses.ToDictionary(
            r => r.Key,
            r => new { description = r.Value.Description ?? "" }
        );

        // Deterministic “edge cases” based on parameter shapes
        var edgeCases = new List<string>();

        foreach (var p in parameters)
        {
            if (p.required)
                edgeCases.Add($"Missing required parameter '{p.name}' should be rejected or handled.");

            if (string.Equals(p.schema.type, "integer", StringComparison.OrdinalIgnoreCase))
                edgeCases.Add($"Parameter '{p.name}' integer boundaries, 0, negative, int32 max, non-numeric.");

            if (string.Equals(p.schema.type, "string", StringComparison.OrdinalIgnoreCase))
                edgeCases.Add($"Parameter '{p.name}' empty string, very long string, unicode, special characters.");
        }

        if (pathKey.Contains("{") && parameters.All(p => p.@in != "Path"))
            edgeCases.Add("Path template includes placeholders but no Path parameters detected, verify OpenAPI spec.");

        var summary = string.IsNullOrWhiteSpace(operation.Summary)
            ? "No summary provided in OpenAPI."
            : operation.Summary.Trim();

        var description = string.IsNullOrWhiteSpace(operation.Description)
            ? ""
            : operation.Description.Trim();

        var requiresAuth = operation.Security is { Count: > 0 };

        return new
        {
            operationId,
            method = httpMethod,
            path = pathKey,
            summary,
            description,
            requiresAuth,
            parameters,
            responses,
            edgeCases
        };
    }
}
