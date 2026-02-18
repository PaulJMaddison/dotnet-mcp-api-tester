namespace ApiTester.Cli;

public static class SecretRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        const int visiblePrefix = 4;
        const int visibleSuffix = 2;

        if (value.Length <= visiblePrefix + visibleSuffix)
        {
            return "***";
        }

        return $"{value[..visiblePrefix]}***{value[^visibleSuffix..]}";
    }
}
