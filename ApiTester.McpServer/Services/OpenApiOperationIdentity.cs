using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public static class OpenApiOperationIdentity
{
    public static string GetEffectiveOperationId(OperationType method, string path, OpenApiOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!string.IsNullOrWhiteSpace(operation.OperationId))
            return operation.OperationId.Trim();

        return $"{method}:{path}";
    }

    public static void EnsureOperationIds(OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var path in document.Paths)
        foreach (var operation in path.Value.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Value.OperationId))
                operation.Value.OperationId = GetEffectiveOperationId(operation.Key, path.Key, operation.Value);
        }
    }

    public static OpenApiOperationMatch? Find(OpenApiDocument document, string operationId)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        foreach (var path in document.Paths)
        foreach (var operation in path.Value.Operations)
        {
            var effectiveId = GetEffectiveOperationId(operation.Key, path.Key, operation.Value);
            if (string.Equals(effectiveId, operationId.Trim(), StringComparison.OrdinalIgnoreCase))
                return new OpenApiOperationMatch(effectiveId, operation.Key, path.Key, path.Value, operation.Value);
        }

        return null;
    }
}

public sealed record OpenApiOperationMatch(
    string OperationId,
    OperationType Method,
    string Path,
    OpenApiPathItem PathItem,
    OpenApiOperation Operation);
