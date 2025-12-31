namespace ApiTester.Ui.Auth;

public sealed class ApiKeyAuthOptions
{
    public const string SectionName = "Auth";

    public string? ApiKey { get; init; }
    public List<string> ApiKeys { get; init; } = new();

    public IReadOnlyList<string> ResolveKeys()
    {
        var keys = new List<string>();

        if (ApiKeys.Count > 0)
            keys.AddRange(ApiKeys);

        if (!string.IsNullOrWhiteSpace(ApiKey))
            keys.Add(ApiKey);

        return keys
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
