namespace ApiTester.Ui.Formatting;

public static class DisplayFormatters
{
    public static string FormatUtc(DateTimeOffset value)
        => value.UtcDateTime.ToString("u");

    public static string FormatUtc(DateTime value)
        => value.ToUniversalTime().ToString("u");

    public static string FormatDurationMs(long durationMs)
        => durationMs > 1000
            ? $"{durationMs / 1000d:0.##} s"
            : $"{durationMs} ms";
}
