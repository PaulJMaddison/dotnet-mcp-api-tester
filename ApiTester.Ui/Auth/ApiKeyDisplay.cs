namespace ApiTester.Ui.Auth;

public static class ApiKeyDisplay
{
    public static string Mask(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "Not signed in";
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 6)
        {
            return new string('•', trimmed.Length);
        }

        var prefix = trimmed[..3];
        var suffix = trimmed[^3..];
        return $"{prefix}{new string('•', trimmed.Length - 6)}{suffix}";
    }
}
