using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Models;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class TestPlanFactoryTests
{
    [Fact]
    public void Create_IncludesMissingRequiredPathParamCase()
    {
        var op = new OpenApiOperation
        {
            OperationId = "getPet",
            Parameters = new List<OpenApiParameter>
            {
                new()
                {
                    Name = "petId",
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema { Type = "integer" }
                }
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse()
            }
        };

        var plan = TestPlanFactory.Create(op, OperationType.Get, "/pets/{petId}", "getPet");

        var missingCase = plan.Cases.Single(c => c.Name == "Missing required path param 'petId'");
        Assert.Empty(missingCase.PathParams);

        var happyCase = plan.Cases.Single(c => c.Name == "Happy path");
        Assert.True(happyCase.PathParams.ContainsKey("petId"));
    }
}
