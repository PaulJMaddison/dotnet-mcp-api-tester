namespace ApiTester.Web.Execution;

public sealed class ExecutionOptions
{
    public string? BaseUrl { get; init; }
    public bool? DryRun { get; init; }
    public bool HostedMode { get; init; }
    public List<string> AllowedBaseUrls { get; init; } = new();
    public List<string> AllowedMethods { get; init; } = new();
    public bool? BlockLocalhost { get; init; }
    public bool? BlockPrivateNetworks { get; init; }
    public int? TimeoutSeconds { get; init; }
    public int? MaxRequestBodyBytes { get; init; }
    public int? MaxResponseBodyBytes { get; init; }
}
