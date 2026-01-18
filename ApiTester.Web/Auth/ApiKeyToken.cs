using System.Security.Cryptography;

namespace ApiTester.Web.Auth;

public static class ApiKeyToken
{
    private const char Separator = '.';
    private const int PrefixBytes = 4;
    private const int SecretBytes = 24;

    public static ApiKeyTokenResult Generate()
    {
        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(PrefixBytes)).ToLowerInvariant();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretBytes)).ToLowerInvariant();
        var token = $"{prefix}{Separator}{secret}";
        return new ApiKeyTokenResult(prefix, token);
    }

    public static bool TryGetPrefix(string? apiKey, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        apiKey = apiKey.Trim();
        var separatorIndex = apiKey.IndexOf(Separator);
        if (separatorIndex <= 0)
            return false;

        prefix = apiKey[..separatorIndex];
        return !string.IsNullOrWhiteSpace(prefix);
    }
}

public sealed record ApiKeyTokenResult(string Prefix, string Token);
