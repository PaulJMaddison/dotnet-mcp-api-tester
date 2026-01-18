namespace ApiTester.Web.Contracts;

public sealed record RunAuditResponse(
    Guid RunId,
    string Actor,
    RunEnvironmentSnapshot? Environment,
    RunPolicySnapshot? PolicySnapshot);

public sealed record RunEnvironmentSnapshot(string? Name, string? BaseUrl);

public sealed record RunPolicySnapshot(
    bool DryRun,
    IReadOnlyList<string> AllowedBaseUrls,
    IReadOnlyList<string> AllowedMethods,
    bool BlockLocalhost,
    bool BlockPrivateNetworks,
    int TimeoutSeconds,
    int MaxRequestBodyBytes,
    int MaxResponseBodyBytes,
    bool RetryOnFlake,
    int MaxRetries);
