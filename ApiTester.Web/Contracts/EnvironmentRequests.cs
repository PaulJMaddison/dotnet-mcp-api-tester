namespace ApiTester.Web.Contracts;

public sealed record EnvironmentCreateRequest(string? Name, string? BaseUrl);

public sealed record EnvironmentUpdateRequest(string? Name, string? BaseUrl);
