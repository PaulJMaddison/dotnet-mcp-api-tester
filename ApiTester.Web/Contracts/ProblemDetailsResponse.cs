namespace ApiTester.Web.Contracts;

public sealed record ProblemDetailsResponse(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance);
