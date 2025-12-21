using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

[McpServerToolType]
public sealed class CoreTools
{
    private readonly AppConfig _config;

    public CoreTools(AppConfig config) => _config = config;

    [McpServerTool, Description("Sanity check tool, returns server info.")]
    public string ApiPing()
    {
        var result = new
        {
            ok = true,
            workingDirectory = _config.WorkingDirectory,
            maxSeconds = _config.MaxSeconds,
            utcNow = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
