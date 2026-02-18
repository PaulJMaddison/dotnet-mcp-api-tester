namespace ApiTester.Web.Contracts;

public sealed record ApiPolicyResponse(
    bool HostedMode,
    bool DryRun,
    IReadOnlyList<string> AllowedBaseUrls,
    IReadOnlyList<string> AllowedMethods,
    int TimeoutSeconds,
    int MaxRequestBodyBytes,
    int MaxResponseBodyBytes,
    bool ValidateSchema,
    bool BlockLocalhost,
    bool BlockPrivateNetworks,
    bool RetryOnFlake,
    int MaxRetries);

public sealed record ApiPolicyUpdateRequest(
    bool? HostedMode,
    bool? DryRun,
    IReadOnlyList<string>? AllowedBaseUrls,
    IReadOnlyList<string>? AllowedMethods,
    int? TimeoutSeconds,
    int? MaxRequestBodyBytes,
    int? MaxResponseBodyBytes,
    bool? ValidateSchema,
    bool? BlockLocalhost,
    bool? BlockPrivateNetworks,
    bool? RetryOnFlake,
    int? MaxRetries);

public sealed record ApiRuntimeBaseUrlRequest(string BaseUrl);

public sealed record ApiRuntimeBaseUrlResponse(string? BaseUrl);

public sealed record ApiRuntimeBearerTokenRequest(string Token);

public sealed record ApiRuntimeAuthResponse(bool Ok, bool HasBearerToken);

public sealed record ApiRuntimeResetResponse(string? BaseUrl, bool HasBearerToken, ApiPolicyResponse Policy);
