using System.ComponentModel;
using ApiTester.McpServer.Services;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class ApiAssistTools
{
    private readonly OpenApiStore _store;
    private readonly OpenApiConstraintTestGenerator _generator;

    public ApiAssistTools(OpenApiStore store, OpenApiConstraintTestGenerator generator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    [McpServerTool, Description("Generate a deterministic, constraint-driven API test plan for an operationId using the OpenAPI contract. Includes parameters, schema bounds and concrete edge-case inputs.")]
    public object ApiGenerateTestPlan(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        var document = _store.RequireDocument();
        var plan = _generator.Generate(document, operationId.Trim());

        return new
        {
            operationId = plan.OperationId,
            method = plan.Method,
            path = plan.Path,
            summary = plan.Summary,
            description = plan.Description,
            requiresAuth = plan.RequiresAuth,
            parameters = plan.Parameters.Select(p => new
            {
                name = p.Name,
                @in = p.Location,
                required = p.Required,
                schema = new
                {
                    type = p.Type,
                    format = p.Format,
                    nullable = p.Nullable,
                    minimum = p.Minimum,
                    maximum = p.Maximum,
                    minLength = p.MinLength,
                    maxLength = p.MaxLength,
                    minItems = p.MinItems,
                    maxItems = p.MaxItems,
                    pattern = p.Pattern,
                    @enum = p.EnumValues
                }
            }).ToList(),
            responses = plan.Responses,
            testCases = plan.TestCases.Select(test => new
            {
                category = test.Category,
                target = test.Target,
                description = test.Description,
                inputs = test.Inputs
            }).ToList(),
            testCaseCount = plan.TestCases.Count
        };
    }
}
