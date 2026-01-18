using ApiTester.McpServer.Models;
using Microsoft.OpenApi.Models;

namespace ApiTester.Web.AI;

public sealed record AiAnalysisInput(
    OrganisationRecord Organisation,
    Guid ProjectId,
    string OperationId,
    string HttpMethod,
    string Path,
    OpenApiOperation Operation,
    ApiExecutionPolicySnapshot Policy,
    TestRunRecord Run);
