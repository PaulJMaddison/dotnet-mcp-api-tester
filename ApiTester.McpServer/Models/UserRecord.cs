using System.Text.Json.Serialization;

namespace ApiTester.McpServer.Models;

public sealed record UserRecord
{
    public Guid UserId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public DateTime CreatedUtc { get; init; }

    [JsonConstructor]
    public UserRecord(Guid userId, string externalId, string displayName, string? email, DateTime createdUtc)
    {
        UserId = userId;
        ExternalId = externalId;
        DisplayName = displayName;
        Email = email;
        CreatedUtc = createdUtc;
    }
}
