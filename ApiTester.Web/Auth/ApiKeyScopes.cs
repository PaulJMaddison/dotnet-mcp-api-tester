namespace ApiTester.Web.Auth;

public static class ApiKeyScopes
{
    public const string RunsRead = "runs:read";
    public const string RunsWrite = "runs:write";
    public const string ProjectsRead = "projects:read";
    public const string ProjectsWrite = "projects:write";
    public const string AdminKeys = "admin:keys";

    private static readonly HashSet<string> KnownScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        RunsRead,
        RunsWrite,
        ProjectsRead,
        ProjectsWrite,
        AdminKeys
    };

    public static IReadOnlySet<string> Parse(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(
            scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(scope => scope.ToLowerInvariant())
                .Where(scope => !string.IsNullOrWhiteSpace(scope)),
            StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryNormalize(IEnumerable<string>? scopes, out IReadOnlyList<string> normalizedScopes, out string error)
    {
        error = string.Empty;
        normalizedScopes = Array.Empty<string>();

        if (scopes is null)
        {
            error = "Scopes are required.";
            return false;
        }

        var list = scopes
            .Select(scope => (scope ?? string.Empty).Trim().ToLowerInvariant())
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count == 0)
        {
            error = "At least one scope is required.";
            return false;
        }

        var invalid = list.Where(scope => !KnownScopes.Contains(scope)).ToList();
        if (invalid.Count > 0)
        {
            error = $"Unknown scopes: {string.Join(", ", invalid)}.";
            return false;
        }

        normalizedScopes = list;
        return true;
    }

    public static string Serialize(IEnumerable<string> scopes)
        => string.Join(',', scopes.Select(scope => scope.Trim().ToLowerInvariant()));
}
