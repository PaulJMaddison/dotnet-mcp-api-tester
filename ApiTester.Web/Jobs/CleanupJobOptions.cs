namespace ApiTester.Web.Jobs;

public sealed class CleanupJobOptions
{
    public bool Enabled { get; set; } = true;
    public int RetentionIntervalMinutes { get; set; } = 60;
    public int SnippetTrimIntervalMinutes { get; set; } = 120;
    public int ResponseSnippetMaxChars { get; set; } = 4096;
    public int StartupDelaySeconds { get; set; } = 30;
}
