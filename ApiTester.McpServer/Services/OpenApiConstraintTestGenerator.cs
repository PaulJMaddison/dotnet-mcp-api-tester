using System.Globalization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed record GeneratedApiTestCase(
    string Category,
    string Target,
    string Description,
    IReadOnlyDictionary<string, string?> Inputs);

public sealed record GeneratedParameterDescriptor(
    string Name,
    string Location,
    bool Required,
    string Type,
    string Format,
    bool Nullable,
    decimal? Minimum,
    decimal? Maximum,
    int? MinLength,
    int? MaxLength,
    int? MinItems,
    int? MaxItems,
    string Pattern,
    IReadOnlyList<string> EnumValues);

public sealed record GeneratedOperationTestPlan(
    string OperationId,
    string Method,
    string Path,
    string Summary,
    string Description,
    bool RequiresAuth,
    IReadOnlyList<GeneratedParameterDescriptor> Parameters,
    IReadOnlyDictionary<string, string> Responses,
    IReadOnlyList<GeneratedApiTestCase> TestCases);

/// <summary>
/// Generates deterministic boundary and contract tests from OpenAPI constraints.
/// The LLM is deliberately not involved in cases the contract can derive exactly.
/// </summary>
public sealed class OpenApiConstraintTestGenerator
{
    public GeneratedOperationTestPlan Generate(OpenApiDocument document, string operationId)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        var found = OpenApiOperationIdentity.Find(document, operationId.Trim())
            ?? throw new InvalidOperationException($"operationId not found: {operationId}");

        var parameters = MergeParameters(found.PathItem.Parameters, found.Operation.Parameters);
        var descriptors = parameters.Select(p => ToDescriptor(p, document)).ToList();
        var cases = new List<GeneratedApiTestCase>();

        foreach (var parameter in parameters)
            AddParameterCases(parameter, document, cases);

        AddRequestBodyCases(found.Operation.RequestBody, document, cases);

        if (found.Path.Contains('{') && parameters.All(p => p.In != ParameterLocation.Path))
            cases.Add(Case("contract-consistency", found.Path, "Path template contains placeholders but no OpenAPI path parameters are declared."));

        var responses = found.Operation.Responses
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .ToDictionary(r => r.Key, r => r.Value.Description ?? string.Empty, StringComparer.Ordinal);

        return new GeneratedOperationTestPlan(
            found.OperationId,
            found.Method.ToString().ToUpperInvariant(),
            found.Path,
            string.IsNullOrWhiteSpace(found.Operation.Summary) ? "No summary provided in OpenAPI." : found.Operation.Summary.Trim(),
            found.Operation.Description?.Trim() ?? string.Empty,
            OpenApiSecuritySemantics.RequiresAuthentication(document, found.Operation),
            descriptors,
            responses,
            Deduplicate(cases));
    }

    private static void AddParameterCases(OpenApiParameter parameter, OpenApiDocument document, List<GeneratedApiTestCase> cases)
    {
        var schema = ResolveSchema(parameter.Schema, document);
        var target = $"{parameter.In}:{parameter.Name}";

        if (parameter.Required)
            cases.Add(Case("required", target, $"Omit required parameter '{parameter.Name}'.", parameter.Name, null));
        else
            cases.Add(Case("optional", target, $"Omit optional parameter '{parameter.Name}'.", parameter.Name, null));

        if (schema.Nullable)
            cases.Add(Case("nullable", target, $"Send explicit null for nullable parameter '{parameter.Name}'.", parameter.Name, "null"));

        switch (EffectiveType(schema))
        {
            case "integer": AddNumericCases(parameter.Name, target, schema, cases, integral: true); break;
            case "number": AddNumericCases(parameter.Name, target, schema, cases, integral: false); break;
            case "boolean":
                cases.Add(Case("boolean", target, $"Use true for '{parameter.Name}'.", parameter.Name, "true"));
                cases.Add(Case("boolean", target, $"Use false for '{parameter.Name}'.", parameter.Name, "false"));
                cases.Add(Case("wrong-type", target, $"Use a non-boolean value for '{parameter.Name}'.", parameter.Name, "not-a-boolean"));
                break;
            case "array": AddArrayCases(parameter.Name, target, schema, cases); break;
            case "object": AddObjectCases(parameter.Name, target, schema, document, cases); break;
            default: AddStringCases(parameter.Name, target, schema, cases); break;
        }

        AddEnumCases(parameter.Name, target, schema, cases);
    }

    private static void AddNumericCases(string name, string target, OpenApiSchema schema, List<GeneratedApiTestCase> cases, bool integral)
    {
        cases.Add(Case("numeric-zero", target, $"Use zero for '{name}'.", name, "0"));
        cases.Add(Case("numeric-negative", target, $"Use a negative value for '{name}'.", name, "-1"));
        cases.Add(Case("wrong-type", target, $"Use a non-numeric value for '{name}'.", name, "not-a-number"));

        if (integral)
        {
            if (string.Equals(schema.Format, "int64", StringComparison.OrdinalIgnoreCase))
            {
                cases.Add(Case("numeric-extreme", target, $"Use Int64.MaxValue for '{name}'.", name, long.MaxValue.ToString(CultureInfo.InvariantCulture)));
                cases.Add(Case("numeric-extreme", target, $"Use Int64.MinValue for '{name}'.", name, long.MinValue.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                cases.Add(Case("numeric-extreme", target, $"Use Int32.MaxValue for '{name}'.", name, int.MaxValue.ToString(CultureInfo.InvariantCulture)));
                cases.Add(Case("numeric-extreme", target, $"Use Int32.MinValue for '{name}'.", name, int.MinValue.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (schema.Minimum is not null)
        {
            var step = integral ? 1m : 0.01m;
            cases.Add(Case("below-minimum", target, $"Use a value just below minimum {schema.Minimum.Value} for '{name}'.", name, FormatDecimal(schema.Minimum.Value - step)));
            cases.Add(Case("minimum", target, $"Use exact minimum for '{name}'.", name, FormatDecimal(schema.Minimum.Value)));
            cases.Add(Case("above-minimum", target, $"Use a value just above minimum for '{name}'.", name, FormatDecimal(schema.Minimum.Value + step)));
        }

        if (schema.Maximum is not null)
        {
            var step = integral ? 1m : 0.01m;
            cases.Add(Case("below-maximum", target, $"Use a value just below maximum for '{name}'.", name, FormatDecimal(schema.Maximum.Value - step)));
            cases.Add(Case("maximum", target, $"Use exact maximum for '{name}'.", name, FormatDecimal(schema.Maximum.Value)));
            cases.Add(Case("above-maximum", target, $"Use a value just above maximum {schema.Maximum.Value} for '{name}'.", name, FormatDecimal(schema.Maximum.Value + step)));
        }
    }

    private static void AddStringCases(string name, string target, OpenApiSchema schema, List<GeneratedApiTestCase> cases)
    {
        cases.Add(Case("string-empty", target, $"Use an empty string for '{name}'.", name, string.Empty));
        cases.Add(Case("string-unicode", target, $"Use Unicode text for '{name}'.", name, "Zażółć-東京-🙂"));
        cases.Add(Case("string-special", target, $"Use reserved/special characters for '{name}'.", name, "'\"<>%{}[]?&=/\\"));

        if (schema.MinLength is not null)
        {
            var min = schema.MinLength.Value;
            if (min > 0) cases.Add(Case("below-min-length", target, $"Use length {min - 1} for '{name}'.", name, new string('a', min - 1)));
            cases.Add(Case("min-length", target, $"Use exact minLength {min} for '{name}'.", name, new string('a', min)));
            cases.Add(Case("above-min-length", target, $"Use length {min + 1} for '{name}'.", name, new string('a', min + 1)));
        }

        if (schema.MaxLength is not null)
        {
            var max = schema.MaxLength.Value;
            if (max > 0) cases.Add(Case("below-max-length", target, $"Use length {max - 1} for '{name}'.", name, new string('b', max - 1)));
            cases.Add(Case("max-length", target, $"Use exact maxLength {max} for '{name}'.", name, new string('b', max)));
            cases.Add(Case("above-max-length", target, $"Use length {max + 1} for '{name}'.", name, new string('b', max + 1)));
        }

        if (!string.IsNullOrWhiteSpace(schema.Pattern))
            cases.Add(Case("pattern-invalid", target, $"Use a value intended not to match pattern '{schema.Pattern}' for '{name}'.", name, "pattern mismatch !"));

        AddFormatCases(name, target, schema.Format, cases);
    }

    private static void AddArrayCases(string name, string target, OpenApiSchema schema, List<GeneratedApiTestCase> cases)
    {
        cases.Add(Case("array-empty", target, $"Use an empty array for '{name}'.", name, "[]"));
        cases.Add(Case("wrong-type", target, $"Use a scalar instead of an array for '{name}'.", name, "not-an-array"));

        if (schema.MinItems is not null)
        {
            var min = schema.MinItems.Value;
            if (min > 0) cases.Add(Case("below-min-items", target, $"Use {min - 1} item(s) for '{name}'.", name, ArrayOf(min - 1)));
            cases.Add(Case("min-items", target, $"Use exact minItems {min} for '{name}'.", name, ArrayOf(min)));
            cases.Add(Case("above-min-items", target, $"Use {min + 1} item(s) for '{name}'.", name, ArrayOf(min + 1)));
        }

        if (schema.MaxItems is not null)
        {
            var max = schema.MaxItems.Value;
            if (max > 0) cases.Add(Case("below-max-items", target, $"Use {max - 1} item(s) for '{name}'.", name, ArrayOf(max - 1)));
            cases.Add(Case("max-items", target, $"Use exact maxItems {max} for '{name}'.", name, ArrayOf(max)));
            cases.Add(Case("above-max-items", target, $"Use {max + 1} item(s) for '{name}'.", name, ArrayOf(max + 1)));
        }
    }

    private static void AddObjectCases(string name, string target, OpenApiSchema schema, OpenApiDocument document, List<GeneratedApiTestCase> cases)
    {
        cases.Add(Case("object-empty", target, $"Use an empty object for '{name}'.", name, "{}"));
        cases.Add(Case("wrong-type", target, $"Use a scalar instead of an object for '{name}'.", name, "not-an-object"));

        foreach (var property in schema.Properties ?? new Dictionary<string, OpenApiSchema>())
        {
            var resolved = ResolveSchema(property.Value, document);
            if (schema.Required?.Contains(property.Key) == true)
            {
                cases.Add(Case(
                    "object-required-property",
                    $"{target}.{property.Key}",
                    $"Omit required nested property '{property.Key}' from object '{name}'.",
                    name,
                    "{}"));
            }

            if (EffectiveType(resolved) == "object")
            {
                cases.Add(Case(
                    "object-nested",
                    $"{target}.{property.Key}",
                    $"Exercise nested object property '{property.Key}' within '{name}'.",
                    name,
                    "{}"));
            }
        }
    }

    private static void AddEnumCases(string name, string target, OpenApiSchema schema, List<GeneratedApiTestCase> cases)
    {
        if (schema.Enum is not { Count: > 0 }) return;
        foreach (var value in schema.Enum.Select(FormatAny))
            cases.Add(Case("enum-valid", target, $"Use documented enum value '{value}' for '{name}'.", name, value));
        cases.Add(Case("enum-invalid", target, $"Use an undocumented enum value for '{name}'.", name, "__invalid_enum_value__"));
        cases.Add(Case("enum-case", target, $"Change casing of a documented enum value for '{name}' where meaningful.", name, ToggleCase(FormatAny(schema.Enum[0]))));
    }

    private static void AddFormatCases(string name, string target, string? format, List<GeneratedApiTestCase> cases)
    {
        switch ((format ?? string.Empty).ToLowerInvariant())
        {
            case "email":
                cases.Add(Case("format-valid", target, $"Use a valid email for '{name}'.", name, "person@example.com"));
                cases.Add(Case("format-invalid", target, $"Use an invalid email for '{name}'.", name, "not-an-email"));
                break;
            case "uuid":
                cases.Add(Case("format-valid", target, $"Use a valid UUID for '{name}'.", name, "11111111-2222-3333-4444-555555555555"));
                cases.Add(Case("format-invalid", target, $"Use an invalid UUID for '{name}'.", name, "not-a-uuid"));
                break;
            case "date":
                cases.Add(Case("format-valid", target, $"Use a valid ISO date for '{name}'.", name, "2026-09-14"));
                cases.Add(Case("format-invalid", target, $"Use an invalid calendar date for '{name}'.", name, "2026-02-31"));
                break;
            case "date-time":
                cases.Add(Case("format-valid", target, $"Use a valid RFC3339 timestamp for '{name}'.", name, "2026-09-14T14:00:00Z"));
                cases.Add(Case("format-invalid", target, $"Use an invalid timestamp for '{name}'.", name, "not-a-date-time"));
                break;
            case "uri":
            case "url":
                cases.Add(Case("format-valid", target, $"Use a valid HTTPS URI for '{name}'.", name, "https://example.com/resource"));
                cases.Add(Case("format-invalid", target, $"Use a malformed URI for '{name}'.", name, "://bad-uri"));
                break;
        }
    }

    private static void AddRequestBodyCases(OpenApiRequestBody? body, OpenApiDocument document, List<GeneratedApiTestCase> cases)
    {
        if (body is null) return;
        if (body.Required) cases.Add(Case("required-body", "requestBody", "Omit the required request body."));

        foreach (var content in body.Content)
        {
            var schema = ResolveSchema(content.Value.Schema, document);
            if (EffectiveType(schema) == "array")
            {
                var before = cases.Count;
                AddArrayCases("requestBody", "requestBody", schema, cases);
                AppendContentType(cases, before, content.Key);
                continue;
            }

            AddObjectProperties(schema, "requestBody", content.Key, document, cases, rootProperties: true, depth: 0);
        }
    }

    private static void AddObjectProperties(
        OpenApiSchema schema,
        string parentTarget,
        string contentType,
        OpenApiDocument document,
        List<GeneratedApiTestCase> cases,
        bool rootProperties,
        int depth)
    {
        if (depth > 12) return;

        foreach (var shape in ObjectShapes(schema, document))
        {
            foreach (var property in shape.Properties ?? new Dictionary<string, OpenApiSchema>())
            {
                var target = $"{parentTarget}.{property.Key}";
                var required = shape.Required?.Contains(property.Key) == true;
                AddBodyValueCases(
                    property.Key,
                    property.Value,
                    target,
                    contentType,
                    required,
                    rootProperties,
                    document,
                    cases,
                    depth + 1);
            }
        }
    }

    private static void AddBodyValueCases(
        string name,
        OpenApiSchema schema,
        string target,
        string contentType,
        bool required,
        bool rootProperty,
        OpenApiDocument document,
        List<GeneratedApiTestCase> cases,
        int depth)
    {
        var resolved = ResolveSchema(schema, document);
        if (required)
        {
            cases.Add(Case(
                rootProperty ? "required-body-property" : "object-required-property",
                rootProperty ? $"requestBody:{name}" : target,
                rootProperty
                    ? $"Omit required request-body property '{name}'. Content-Type: {contentType}."
                    : $"Omit required nested property '{name}'. Content-Type: {contentType}.",
                name,
                null));
        }

        var objectShapes = ObjectShapes(resolved, document).ToList();
        if (objectShapes.Count > 0)
        {
            cases.Add(Case("object-empty", target, $"Use an empty object for '{name}'. Content-Type: {contentType}.", name, "{}"));
            cases.Add(Case("wrong-type", target, $"Use a scalar instead of an object for '{name}'. Content-Type: {contentType}.", name, "not-an-object"));
            AddObjectProperties(resolved, target, contentType, document, cases, rootProperties: false, depth);
            return;
        }

        var before = cases.Count;
        switch (EffectiveType(resolved))
        {
            case "integer": AddNumericCases(name, target, resolved, cases, integral: true); break;
            case "number": AddNumericCases(name, target, resolved, cases, integral: false); break;
            case "boolean":
                cases.Add(Case("boolean", target, $"Use true for '{name}'.", name, "true"));
                cases.Add(Case("boolean", target, $"Use false for '{name}'.", name, "false"));
                cases.Add(Case("wrong-type", target, $"Use a non-boolean value for '{name}'.", name, "not-a-boolean"));
                break;
            case "array": AddArrayCases(name, target, resolved, cases); break;
            default: AddStringCases(name, target, resolved, cases); break;
        }
        AddEnumCases(name, target, resolved, cases);
        AppendContentType(cases, before, contentType);
    }

    private static IEnumerable<OpenApiSchema> ObjectShapes(OpenApiSchema schema, OpenApiDocument document)
    {
        var resolved = ResolveSchema(schema, document);
        var hasComposition = resolved.AllOf is { Count: > 0 } || resolved.OneOf is { Count: > 0 };

        if (EffectiveType(resolved) == "object" && resolved.Properties is { Count: > 0 })
            yield return resolved;

        if (resolved.AllOf is { Count: > 0 })
        {
            foreach (var part in resolved.AllOf)
            foreach (var shape in ObjectShapes(part, document))
                yield return shape;
        }

        if (resolved.OneOf is { Count: > 0 })
        {
            foreach (var part in resolved.OneOf)
            foreach (var shape in ObjectShapes(part, document))
                yield return shape;
        }

        if (!hasComposition && EffectiveType(resolved) == "object" && resolved.Properties is not { Count: > 0 })
            yield return resolved;
    }

    private static void AppendContentType(List<GeneratedApiTestCase> cases, int start, string contentType)
    {
        for (var index = start; index < cases.Count; index++)
            cases[index] = cases[index] with { Description = cases[index].Description + $" Content-Type: {contentType}." };
    }

    private static OpenApiSchema ResolveSchema(OpenApiSchema? schema, OpenApiDocument document)
    {
        if (schema is null) return new OpenApiSchema();

        var current = schema;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrWhiteSpace(current.Reference?.Id) &&
               visited.Add(current.Reference.Id) &&
               document.Components?.Schemas is not null &&
               document.Components.Schemas.TryGetValue(current.Reference.Id, out var resolved))
        {
            current = resolved;
        }

        return current;
    }

    private static string EffectiveType(OpenApiSchema schema)
    {
        if (!string.IsNullOrWhiteSpace(schema.Type))
            return schema.Type.ToLowerInvariant();
        if (schema.Properties is { Count: > 0 })
            return "object";
        if (schema.Items is not null)
            return "array";
        if (schema.AllOf is { Count: > 0 } || schema.OneOf is { Count: > 0 })
            return "object";
        return "string";
    }

    private static GeneratedParameterDescriptor ToDescriptor(OpenApiParameter parameter, OpenApiDocument document)
    {
        var schema = ResolveSchema(parameter.Schema, document);
        return new GeneratedParameterDescriptor(
            parameter.Name,
            parameter.In.ToString() ?? string.Empty,
            parameter.Required,
            EffectiveType(schema),
            schema.Format ?? string.Empty,
            schema.Nullable,
            schema.Minimum,
            schema.Maximum,
            schema.MinLength,
            schema.MaxLength,
            schema.MinItems,
            schema.MaxItems,
            schema.Pattern ?? string.Empty,
            schema.Enum?.Select(FormatAny).ToList() ?? new List<string>());
    }

    private static IReadOnlyList<OpenApiParameter> MergeParameters(IList<OpenApiParameter>? path, IList<OpenApiParameter>? operation)
    {
        var merged = new Dictionary<string, OpenApiParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in path ?? Array.Empty<OpenApiParameter>()) merged[$"{parameter.In}:{parameter.Name}"] = parameter;
        foreach (var parameter in operation ?? Array.Empty<OpenApiParameter>()) merged[$"{parameter.In}:{parameter.Name}"] = parameter;
        return merged.Values.OrderBy(p => p.In.ToString(), StringComparer.Ordinal).ThenBy(p => p.Name, StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<GeneratedApiTestCase> Deduplicate(IEnumerable<GeneratedApiTestCase> cases)
        => cases.GroupBy(c => $"{c.Category}|{c.Target}|{c.Description}|{string.Join(";", c.Inputs.Select(i => $"{i.Key}={i.Value}"))}", StringComparer.Ordinal)
            .Select(g => g.First()).ToList();

    private static GeneratedApiTestCase Case(string category, string target, string description)
        => new(category, target, description, new Dictionary<string, string?>());
    private static GeneratedApiTestCase Case(string category, string target, string description, string inputName, string? value)
        => new(category, target, description, new Dictionary<string, string?> { [inputName] = value });

    private static string FormatAny(IOpenApiAny value) => value switch
    {
        OpenApiString s => s.Value,
        OpenApiInteger i => i.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiLong l => l.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiFloat f => f.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiDouble d => d.Value.ToString(CultureInfo.InvariantCulture),
        OpenApiBoolean b => b.Value ? "true" : "false",
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatDecimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string ToggleCase(string value) => string.Concat(value.Select(ch => char.IsLetter(ch) ? (char.IsUpper(ch) ? char.ToLowerInvariant(ch) : char.ToUpperInvariant(ch)) : ch));
    private static string ArrayOf(int count) => "[" + string.Join(",", Enumerable.Range(0, Math.Max(0, count)).Select(i => $"\"item-{i + 1}\"")) + "]";
}
