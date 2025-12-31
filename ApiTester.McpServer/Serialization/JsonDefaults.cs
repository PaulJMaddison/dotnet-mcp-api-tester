using System.Text.Json;

namespace ApiTester.McpServer.Serialization;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
