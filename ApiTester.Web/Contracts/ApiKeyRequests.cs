namespace ApiTester.Web.Contracts;

public sealed record ApiKeyCreateRequest(
    string Name,
    List<string> Scopes,
    DateTime? ExpiresUtc);
