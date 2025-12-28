using Microsoft.Extensions.Configuration;

public sealed class AppConfig
{
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();
    public int MaxSeconds { get; init; } = 60;

    // Day 14: optional SQL persistence
    public string? ConnectionString { get; init; }

    public static AppConfig Load(IConfiguration? configuration = null)
    {
        var workDir = Environment.GetEnvironmentVariable("MCP_WORKDIR");
        var maxSecondsRaw = Environment.GetEnvironmentVariable("MCP_MAX_SECONDS");

        var connectionString =
            configuration?.GetConnectionString("ApiTester")
            ?? Environment.GetEnvironmentVariable("MCP_SQL_CONNECTION");

        return new AppConfig
        {
            WorkingDirectory = string.IsNullOrWhiteSpace(workDir)
                ? Directory.GetCurrentDirectory()
                : workDir,

            MaxSeconds = int.TryParse(maxSecondsRaw, out var s) ? s : 60,

            ConnectionString = string.IsNullOrWhiteSpace(connectionString) ? null : connectionString
        };
    }
}
