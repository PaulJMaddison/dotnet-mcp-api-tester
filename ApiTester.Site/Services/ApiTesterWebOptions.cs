namespace ApiTester.Site.Services;

public sealed class ApiTesterWebOptions
{
    public const string SectionName = "ApiTesterWeb";

    public string BaseUrl { get; init; } = "https://localhost:5001";
}
