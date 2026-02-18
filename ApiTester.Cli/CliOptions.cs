namespace ApiTester.Cli;

public sealed record CliOptions(Uri BaseUrl, string Token, CliCommand Command)
{
    public string RedactedToken => SecretRedactor.Redact(Token);

    public static string BaseUrlEnvVar => "APITESTER_BASE_URL";

    public static string TokenEnvVar => "APITESTER_TOKEN";
}
