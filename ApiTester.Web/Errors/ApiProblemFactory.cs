using Microsoft.AspNetCore.Mvc;

namespace ApiTester.Web.Errors;

public static class ApiProblemFactory
{
    public static ProblemDetails Create(HttpContext context, int statusCode, string errorCode, string title, string detail, string? type = null)
    {
        var correlationId = context.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type ?? $"urn:apitester:error:{errorCode}"
        };

        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["timestampUtc"] = DateTime.UtcNow;

        return problem;
    }

    public static IResult Result(HttpContext context, int statusCode, string errorCode, string title, string detail, string? type = null)
        => Results.Problem(Create(context, statusCode, errorCode, title, detail, type));
}
