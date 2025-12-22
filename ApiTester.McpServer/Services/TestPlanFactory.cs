using ApiTester.McpServer.Models;
using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public static class TestPlanFactory
{
    public static TestPlan Create(OpenApiOperation op, OperationType method, string path, string operationId)
    {
        var plan = new TestPlan
        {
            OperationId = operationId,
            Method = method.ToString().ToUpperInvariant(),
            PathTemplate = path
        };

        // Deterministic plan rules:
        // - Always include a "happy path" if possible
        // - If there are required path params, include boundary and invalid values
        // - Keep it small and predictable

        var pathParams = op.Parameters?
            .Where(p => p.In == ParameterLocation.Path)
            .ToList() ?? new();

        // Case: Happy path
        var happy = new TestCase { Name = "Happy path" };
        foreach (var p in pathParams)
        {
            // basic deterministic defaults based on simple types
            happy.PathParams[p.Name] = DefaultForSchema(p.Schema);
        }
        happy.ExpectedStatusCodes.AddRange(ExpectedFromResponses(op, fallback: 200));
        plan.Cases.Add(happy);

        // If there is a required path param, add extra cases
        foreach (var p in pathParams.Where(x => x.Required))
        {
            // Missing required param: we can’t actually call without it (URL template needs it),
            // but we still record a deterministic case result as "blocked" at runtime.
            plan.Cases.Add(new TestCase
            {
                Name = $"Missing required path param '{p.Name}'",
                ExpectedStatusCodes = new List<int> { 400 }
            });

            // Invalid type
            plan.Cases.Add(new TestCase
            {
                Name = $"Invalid '{p.Name}' type",
                PathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [p.Name] = "abc"
                },
                ExpectedStatusCodes = new List<int> { 400, 404 }
            });

            // Boundary-ish values for ints
            if (IsIntSchema(p.Schema))
            {
                plan.Cases.Add(new TestCase
                {
                    Name = $"Boundary '{p.Name}' = 0",
                    PathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [p.Name] = "0"
                    },
                    ExpectedStatusCodes = ExpectedFromResponses(op, fallback: 200)
                });

                plan.Cases.Add(new TestCase
                {
                    Name = $"Boundary '{p.Name}' = -1",
                    PathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [p.Name] = "-1"
                    },
                    ExpectedStatusCodes = new List<int> { 400, 404 }
                });
            }
        }

        // Keep cases capped
        if (plan.Cases.Count > 8)
            plan.Cases = plan.Cases.Take(8).ToList();

        return plan;
    }

    private static string DefaultForSchema(OpenApiSchema? schema)
    {
        if (schema is null) return "1";

        if (IsIntSchema(schema)) return "200";
        if (string.Equals(schema.Type, "number", StringComparison.OrdinalIgnoreCase)) return "1";
        if (string.Equals(schema.Type, "boolean", StringComparison.OrdinalIgnoreCase)) return "true";

        // default
        return "1";
    }

    private static bool IsIntSchema(OpenApiSchema? schema)
    {
        if (schema is null) return false;
        if (!string.Equals(schema.Type, "integer", StringComparison.OrdinalIgnoreCase)) return false;

        // format int32/int64
        return true;
    }

    private static List<int> ExpectedFromResponses(OpenApiOperation op, int fallback)
    {
        var codes = new List<int>();

        foreach (var kvp in op.Responses ?? new OpenApiResponses())
        {
            if (int.TryParse(kvp.Key, out var code))
                codes.Add(code);
        }

        if (codes.Count == 0)
            codes.Add(fallback);

        // prefer 200 if present
        codes = codes.Distinct().OrderBy(x => x).ToList();
        return codes;
    }
}
