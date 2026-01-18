using System.Security.Cryptography;
using System.Text;

namespace ApiTester.Web.Auth;

public static class ApiKeyHasher
{
    public static string Hash(string apiKey)
    {
        apiKey = (apiKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required.", nameof(apiKey));

        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool Verify(string apiKey, string expectedHash)
    {
        apiKey = (apiKey ?? string.Empty).Trim();
        expectedHash = (expectedHash ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(expectedHash))
            return false;

        try
        {
            var expectedBytes = Convert.FromBase64String(expectedHash);
            var actualBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
