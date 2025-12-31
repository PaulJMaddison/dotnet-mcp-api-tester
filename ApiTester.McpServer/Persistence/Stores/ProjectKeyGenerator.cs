namespace ApiTester.McpServer.Persistence.Stores;

internal static class ProjectKeyGenerator
{
    public static string FromName(string name)
    {
        var s = (name ?? string.Empty).Trim().ToLowerInvariant();
        var chars = s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var key = new string(chars);

        while (key.Contains("--", StringComparison.Ordinal))
            key = key.Replace("--", "-", StringComparison.Ordinal);

        key = key.Trim('-');

        return string.IsNullOrWhiteSpace(key) ? "default" : key;
    }
}
