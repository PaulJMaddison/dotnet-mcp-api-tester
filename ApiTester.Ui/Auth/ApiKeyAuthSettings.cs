namespace ApiTester.Ui.Auth;

public sealed class ApiKeyAuthSettings
{
    private readonly HashSet<string> _allowedKeys;

    public ApiKeyAuthSettings(IEnumerable<string> allowedKeys)
    {
        _allowedKeys = new HashSet<string>(allowedKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    public bool IsAllowed(string apiKey)
        => !string.IsNullOrWhiteSpace(apiKey) && _allowedKeys.Contains(apiKey.Trim());
}
