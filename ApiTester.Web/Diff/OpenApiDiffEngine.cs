using Microsoft.OpenApi.Models;

namespace ApiTester.Web.Diff;

public enum OpenApiDiffClassification
{
    Breaking,
    NonBreaking,
    Informational
}

public static class OpenApiDiffChange
{
    public const string PathAdded = "PathAdded";
    public const string PathRemoved = "PathRemoved";
    public const string MethodAdded = "MethodAdded";
    public const string MethodRemoved = "MethodRemoved";
    public const string ParameterAdded = "ParameterAdded";
    public const string ParameterRemoved = "ParameterRemoved";
    public const string ParameterRequirednessChanged = "ParameterRequirednessChanged";
    public const string ResponseCodeAdded = "ResponseCodeAdded";
    public const string ResponseCodeRemoved = "ResponseCodeRemoved";
    public const string ResponseSchemaChanged = "ResponseSchemaChanged";
    public const string ResponseSchemaChangedUncertain = "ResponseSchemaChangedUncertain";
    public const string NoChanges = "NoChanges";
}

public sealed record OpenApiDiffItem(
    OpenApiDiffClassification Classification,
    string Change,
    string? Path,
    string? Method,
    string Detail);

public sealed record OpenApiDiffResult(IReadOnlyList<OpenApiDiffItem> Items);

public static class OpenApiDiffEngine
{
    public static OpenApiDiffResult Diff(OpenApiDocument before, OpenApiDocument after)
    {
        var diffs = new List<OpenApiDiffItem>();
        var beforePaths = before.Paths ?? new OpenApiPaths();
        var afterPaths = after.Paths ?? new OpenApiPaths();

        foreach (var path in beforePaths.Keys)
        {
            if (!afterPaths.ContainsKey(path))
            {
                diffs.Add(new OpenApiDiffItem(
                    OpenApiDiffClassification.Breaking,
                    OpenApiDiffChange.PathRemoved,
                    path,
                    null,
                    $"Path '{path}' was removed."));
            }
        }

        foreach (var path in afterPaths.Keys)
        {
            if (!beforePaths.ContainsKey(path))
            {
                diffs.Add(new OpenApiDiffItem(
                    OpenApiDiffClassification.NonBreaking,
                    OpenApiDiffChange.PathAdded,
                    path,
                    null,
                    $"Path '{path}' was added."));
            }
        }

        foreach (var path in beforePaths.Keys.Intersect(afterPaths.Keys, StringComparer.Ordinal))
        {
            var beforeItem = beforePaths[path];
            var afterItem = afterPaths[path];

            var beforeOps = beforeItem.Operations ?? new Dictionary<OperationType, OpenApiOperation>();
            var afterOps = afterItem.Operations ?? new Dictionary<OperationType, OpenApiOperation>();

            foreach (var method in beforeOps.Keys)
            {
                if (!afterOps.ContainsKey(method))
                {
                    diffs.Add(new OpenApiDiffItem(
                        OpenApiDiffClassification.Breaking,
                        OpenApiDiffChange.MethodRemoved,
                        path,
                        FormatMethod(method),
                        $"{FormatMethod(method)} operation was removed from '{path}'."));
                }
            }

            foreach (var method in afterOps.Keys)
            {
                if (!beforeOps.ContainsKey(method))
                {
                    diffs.Add(new OpenApiDiffItem(
                        OpenApiDiffClassification.NonBreaking,
                        OpenApiDiffChange.MethodAdded,
                        path,
                        FormatMethod(method),
                        $"{FormatMethod(method)} operation was added to '{path}'."));
                }
            }

            foreach (var method in beforeOps.Keys.Intersect(afterOps.Keys))
            {
                var beforeOp = beforeOps[method];
                var afterOp = afterOps[method];
                CompareParameters(diffs, path, method, beforeItem, beforeOp, afterItem, afterOp);
                CompareResponses(diffs, path, method, beforeOp, afterOp);
            }
        }

        if (diffs.Count == 0)
        {
            diffs.Add(new OpenApiDiffItem(
                OpenApiDiffClassification.Informational,
                OpenApiDiffChange.NoChanges,
                null,
                null,
                "No differences detected."));
        }

        var ordered = diffs
            .OrderBy(diff => diff.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(diff => diff.Method ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(diff => diff.Change, StringComparer.Ordinal)
            .ThenBy(diff => diff.Detail, StringComparer.Ordinal)
            .ToList();

        return new OpenApiDiffResult(ordered);
    }

    private static void CompareParameters(
        ICollection<OpenApiDiffItem> diffs,
        string path,
        OperationType method,
        OpenApiPathItem beforeItem,
        OpenApiOperation beforeOp,
        OpenApiPathItem afterItem,
        OpenApiOperation afterOp)
    {
        var beforeParams = BuildParameterMap(beforeItem, beforeOp);
        var afterParams = BuildParameterMap(afterItem, afterOp);

        foreach (var key in beforeParams.Keys)
        {
            if (!afterParams.ContainsKey(key))
            {
                var param = beforeParams[key];
                diffs.Add(new OpenApiDiffItem(
                    OpenApiDiffClassification.Breaking,
                    OpenApiDiffChange.ParameterRemoved,
                    path,
                    FormatMethod(method),
                    $"Parameter '{param.Name}' in {param.In} was removed."));
            }
        }

        foreach (var key in afterParams.Keys)
        {
            if (!beforeParams.ContainsKey(key))
            {
                var param = afterParams[key];
                var classification = param.Required
                    ? OpenApiDiffClassification.Breaking
                    : OpenApiDiffClassification.NonBreaking;
                diffs.Add(new OpenApiDiffItem(
                    classification,
                    OpenApiDiffChange.ParameterAdded,
                    path,
                    FormatMethod(method),
                    $"Parameter '{param.Name}' in {param.In} was added (required={param.Required.ToString().ToLowerInvariant()})."));
            }
        }

        foreach (var key in beforeParams.Keys.Intersect(afterParams.Keys))
        {
            var beforeParam = beforeParams[key];
            var afterParam = afterParams[key];
            if (beforeParam.Required != afterParam.Required)
            {
                var classification = afterParam.Required
                    ? OpenApiDiffClassification.Breaking
                    : OpenApiDiffClassification.NonBreaking;
                diffs.Add(new OpenApiDiffItem(
                    classification,
                    OpenApiDiffChange.ParameterRequirednessChanged,
                    path,
                    FormatMethod(method),
                    $"Parameter '{afterParam.Name}' in {afterParam.In} changed required from {beforeParam.Required.ToString().ToLowerInvariant()} to {afterParam.Required.ToString().ToLowerInvariant()}."));
            }
        }
    }

    private static void CompareResponses(
        ICollection<OpenApiDiffItem> diffs,
        string path,
        OperationType method,
        OpenApiOperation beforeOp,
        OpenApiOperation afterOp)
    {
        var beforeResponses = beforeOp.Responses?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, OpenApiResponse>(StringComparer.OrdinalIgnoreCase);
        var afterResponses = afterOp.Responses?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, OpenApiResponse>(StringComparer.OrdinalIgnoreCase);
        var beforeCodes = beforeResponses.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var afterCodes = afterResponses.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var code in beforeCodes)
        {
            if (!afterCodes.Contains(code))
            {
                diffs.Add(new OpenApiDiffItem(
                    OpenApiDiffClassification.Breaking,
                    OpenApiDiffChange.ResponseCodeRemoved,
                    path,
                    FormatMethod(method),
                    $"Response code '{code}' was removed."));
            }
        }

        foreach (var code in afterCodes)
        {
            if (!beforeCodes.Contains(code))
            {
                diffs.Add(new OpenApiDiffItem(
                    OpenApiDiffClassification.NonBreaking,
                    OpenApiDiffChange.ResponseCodeAdded,
                    path,
                    FormatMethod(method),
                    $"Response code '{code}' was added."));
            }
        }

        foreach (var code in beforeCodes.Intersect(afterCodes, StringComparer.OrdinalIgnoreCase))
        {
            var beforeResponse = beforeResponses[code];
            var afterResponse = afterResponses[code];
            CompareResponseSchemas(diffs, path, method, code, beforeResponse, afterResponse);
        }
    }

    private static void CompareResponseSchemas(
        ICollection<OpenApiDiffItem> diffs,
        string path,
        OperationType method,
        string code,
        OpenApiResponse beforeResponse,
        OpenApiResponse afterResponse)
    {
        var beforeSchemas = BuildResponseSchemaMap(beforeResponse);
        var afterSchemas = BuildResponseSchemaMap(afterResponse);

        foreach (var contentType in beforeSchemas.Keys.Union(afterSchemas.Keys, StringComparer.OrdinalIgnoreCase))
        {
            beforeSchemas.TryGetValue(contentType, out var beforeSignature);
            afterSchemas.TryGetValue(contentType, out var afterSignature);

            if (string.Equals(beforeSignature, afterSignature, StringComparison.Ordinal))
                continue;

            var schemaChangeClassification = IsSchemaComparisonUncertain(beforeSignature, afterSignature)
                ? OpenApiDiffClassification.Informational
                : OpenApiDiffClassification.Breaking;
            var changeType = schemaChangeClassification == OpenApiDiffClassification.Breaking
                ? OpenApiDiffChange.ResponseSchemaChanged
                : OpenApiDiffChange.ResponseSchemaChangedUncertain;

            diffs.Add(new OpenApiDiffItem(
                schemaChangeClassification,
                changeType,
                path,
                FormatMethod(method),
                $"Response schema for '{code}' ({contentType}) changed."));
        }
    }

    private static bool IsSchemaComparisonUncertain(string? beforeSignature, string? afterSignature)
    {
        return ContainsUncertainMarker(beforeSignature) || ContainsUncertainMarker(afterSignature);
    }

    private static bool ContainsUncertainMarker(string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        return signature.Contains("depth-limit", StringComparison.Ordinal)
            || signature.Contains("recursion", StringComparison.Ordinal);
    }

    private static Dictionary<string, string> BuildResponseSchemaMap(OpenApiResponse response)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (response.Content is null)
            return map;

        foreach (var entry in response.Content)
        {
            var signature = BuildSchemaSignature(entry.Value?.Schema, 0, new HashSet<OpenApiSchema>());
            map[entry.Key] = signature;
        }

        return map;
    }

    private static string BuildSchemaSignature(OpenApiSchema? schema, int depth, HashSet<OpenApiSchema> visited)
    {
        if (schema is null)
            return "none";

        if (!visited.Add(schema))
            return "recursion";

        if (depth > 4)
            return "depth-limit";

        var parts = new List<string>
        {
            $"ref:{schema.Reference?.Id ?? schema.Reference?.ExternalResource ?? string.Empty}",
            $"type:{schema.Type ?? string.Empty}",
            $"format:{schema.Format ?? string.Empty}",
            $"nullable:{schema.Nullable.ToString().ToLowerInvariant()}"
        };

        if (schema.Enum?.Count > 0)
        {
            var enums = schema.Enum.Select(value => value?.ToString() ?? string.Empty).OrderBy(value => value, StringComparer.Ordinal);
            parts.Add($"enum:{string.Join(",", enums)}");
        }

        if (schema.Required?.Count > 0)
        {
            var required = schema.Required.OrderBy(name => name, StringComparer.Ordinal);
            parts.Add($"required:{string.Join(",", required)}");
        }

        if (schema.Properties?.Count > 0)
        {
            var properties = schema.Properties
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{BuildSchemaSignature(pair.Value, depth + 1, visited)}");
            parts.Add($"properties:{string.Join("|", properties)}");
        }

        if (schema.Items is not null)
            parts.Add($"items:{BuildSchemaSignature(schema.Items, depth + 1, visited)}");

        return string.Join("|", parts);
    }

    private static Dictionary<string, OpenApiParameter> BuildParameterMap(OpenApiPathItem pathItem, OpenApiOperation operation)
    {
        var map = new Dictionary<string, OpenApiParameter>(StringComparer.OrdinalIgnoreCase);

        if (pathItem.Parameters is not null)
        {
            foreach (var param in pathItem.Parameters)
            {
                map[MakeParameterKey(param)] = param;
            }
        }

        if (operation.Parameters is not null)
        {
            foreach (var param in operation.Parameters)
            {
                map[MakeParameterKey(param)] = param;
            }
        }

        return map;
    }

    private static string MakeParameterKey(OpenApiParameter parameter)
    {
        var location = parameter.In.ToString();
        location = string.IsNullOrWhiteSpace(location) ? "unknown" : location;
        return $"{location.ToLowerInvariant()}:{parameter.Name}";
    }

    private static string FormatMethod(OperationType method)
        => method.ToString().ToUpperInvariant();
}
