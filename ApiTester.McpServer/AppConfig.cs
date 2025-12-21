public sealed class AppConfig
{
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();
    public int MaxSeconds { get; init; } = 60;

    public static AppConfig Load()
    {
        var workDir = Environment.GetEnvironmentVariable("MCP_WORKDIR");
        var maxSecondsRaw = Environment.GetEnvironmentVariable("MCP_MAX_SECONDS");

        return new AppConfig
        {
            WorkingDirectory = string.IsNullOrWhiteSpace(workDir) ? Directory.GetCurrentDirectory() : workDir,
            MaxSeconds = int.TryParse(maxSecondsRaw, out var s) ? s : 60
        };
    }
}
