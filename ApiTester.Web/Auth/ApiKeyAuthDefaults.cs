namespace ApiTester.Web.Auth;

public static class ApiKeyAuthDefaults
{
    public const string HeaderName = "X-Api-Key";
    public const string AuthorizationHeaderName = "Authorization";
    public const string BearerScheme = "Bearer";
    public const string ApiKeyContextItemName = "ApiKeyContext";
    public const string RawApiKeyItemName = "RawApiKey";
    public const string RawAuthorizationItemName = "RawAuthorization";
    public const string RedactedValue = "[redacted]";
}
