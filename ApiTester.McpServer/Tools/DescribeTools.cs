using System.ComponentModel;
using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class DescribeTools
{
    private readonly OpenApiStore _store;

    public DescribeTools(OpenApiStore store)
    {
        _store = store;
    }

    [McpServerTool, Description("List operations in the loaded OpenAPI document with operation ID, method, path and summary.")]
    public object ApiListOperations()
    {
        var doc = _store.RequireDocument();

        var operations = doc.Paths
            .OrderBy(path => path.Key, StringComparer.Ordinal)
            .SelectMany(path => path.Value.Operations
                .OrderBy(operation => operation.Key.ToString(), StringComparer.Ordinal)
                .Select(operation => new
                {
                    operationId = OpenApiOperationIdentity.GetEffectiveOperationId(operation.Key, path.Key, operation.Value),
                    method = operation.Key.ToString().ToUpperInvariant(),
                    path = path.Key,
                    summary = operation.Value.Summary ?? string.Empty
                }))
            .ToList();

        return new { count = operations.Count, operations };
    }

    [McpServerTool, Description("Describe an OpenAPI operation (method, path, params, request body, responses, security) by operationId.")]
    public object ApiDescribeOperation(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        var doc = _store.RequireDocument();
        var match = OpenApiOperationIdentity.Find(doc, operationId)
            ?? throw new InvalidOperationException($"OperationId not found: {operationId}");

        var operation = match.Operation;
        var parameters = new List<object>();
        foreach (var param in operation.Parameters ?? new List<OpenApiParameter>())
        {
            parameters.Add(new
            {
                name = param.Name,
                @in = param.In.ToString(),
                required = param.Required,
                description = param.Description ?? "",
                schema = DescribeSchema(param.Schema)
            });
        }

        object? requestBody = null;
        if (operation.RequestBody is not null)
        {
            var content = operation.RequestBody.Content;
            requestBody = new
            {
                required = operation.RequestBody.Required,
                description = operation.RequestBody.Description ?? "",
                content = content.ToDictionary(
                    kv => kv.Key,
                    kv => (object)new { schema = DescribeSchema(kv.Value.Schema) }
                )
            };
        }

        var responses = new Dictionary<string, object>();
        foreach (var r in operation.Responses)
        {
            var respContent = r.Value.Content ?? new Dictionary<string, OpenApiMediaType>();
            responses[r.Key] = new
            {
                description = r.Value.Description ?? "",
                content = respContent.ToDictionary(
                    kv => kv.Key,
                    kv => (object)new { schema = DescribeSchema(kv.Value.Schema) }
                )
            };
        }

        var requiresAuth = operation.Security is { Count: > 0 } || doc.SecurityRequirements is { Count: > 0 };

        return new
        {
            operationId = match.OperationId,
            method = match.Method.ToString().ToUpperInvariant(),
            path = match.Path,
            summary = operation.Summary ?? "",
            description = operation.Description ?? "",
            requiresAuth,
            parameters,
            requestBody,
            responses
        };
    }

    private static object? DescribeSchema(OpenApiSchema? schema)
    {
        if (schema is null) return null;

        return new
        {
            type = schema.Type ?? "",
            format = schema.Format ?? "",
            nullable = schema.Nullable,
            items = schema.Items is null ? null : new { type = schema.Items.Type ?? "", format = schema.Items.Format ?? "" }
        };
    }
}
