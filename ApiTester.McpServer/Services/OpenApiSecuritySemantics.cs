using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using System.Text.Json;

namespace ApiTester.McpServer.Services;

public static class OpenApiSecuritySemantics
{
    private const string ExplicitNoSecurityExtension = "x-apitester-explicit-no-security";

    public static void PreserveExplicitOverrides(OpenApiDocument document, string sourceText)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(sourceText)) return;

        try
        {
            using var source = JsonDocument.Parse(sourceText);
            if (!source.RootElement.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
                return;

            foreach (var pathProperty in paths.EnumerateObject())
            {
                if (!document.Paths.TryGetValue(pathProperty.Name, out var pathItem) || pathProperty.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var operationProperty in pathProperty.Value.EnumerateObject())
                {
                    if (!Enum.TryParse<OperationType>(operationProperty.Name, true, out var method) ||
                        !pathItem.Operations.TryGetValue(method, out var operation) ||
                        operationProperty.Value.ValueKind != JsonValueKind.Object ||
                        !operationProperty.Value.TryGetProperty("security", out var security) ||
                        security.ValueKind != JsonValueKind.Array ||
                        security.GetArrayLength() != 0)
                        continue;

                    operation.Extensions[ExplicitNoSecurityExtension] = new OpenApiBoolean(true);
                }
            }
        }
        catch (JsonException)
        {
            // YAML is still parsed by OpenApiStringReader. JSON presence tracking is
            // best-effort because OpenAPI.NET 1.x discards omitted-vs-empty list state.
        }
    }

    public static IList<OpenApiSecurityRequirement>? EffectiveRequirements(
        OpenApiDocument document,
        OpenApiOperation operation)
    {
        if (operation.Extensions.ContainsKey(ExplicitNoSecurityExtension))
            return Array.Empty<OpenApiSecurityRequirement>();
        return operation.Security is { Count: > 0 }
            ? operation.Security
            : document.SecurityRequirements;
    }

    public static bool RequiresAuthentication(OpenApiDocument document, OpenApiOperation operation)
        => EffectiveRequirements(document, operation) is { Count: > 0 };
}
