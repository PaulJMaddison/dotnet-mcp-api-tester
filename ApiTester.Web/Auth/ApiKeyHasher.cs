using System.Security.Cryptography;
using System.Text;

namespace ApiTester.Web.Auth;

public static class ApiKeyHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static string Hash(string apiKey)
    {
        apiKey = (apiKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required.", nameof(apiKey));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(apiKey), salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string apiKey, string expectedHash)
    {
        apiKey = (apiKey ?? string.Empty).Trim();
        expectedHash = (expectedHash ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(expectedHash))
            return false;

        var parts = expectedHash.Split('$');
        if (parts.Length != 4 || !parts[0].Equals("pbkdf2-sha256", StringComparison.Ordinal))
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedBytes = Convert.FromBase64String(parts[3]);
            var actualBytes = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(apiKey), salt, iterations, HashAlgorithmName.SHA256, expectedBytes.Length);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
