using System.Text.Json;

namespace ApiTester.McpServer.Services;

public sealed class QualificationTelemetry : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter? _writer;

    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
    public bool Enabled => _writer is not null;

    public QualificationTelemetry()
        : this(Environment.GetEnvironmentVariable("API_TESTER_QUALIFICATION_DIR"))
    {
    }

    public QualificationTelemetry(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Directory.CreateDirectory(directory);
        _writer = new StreamWriter(Path.Combine(directory, "telemetry.ndjson"), append: true) { AutoFlush = true };
        Emit("server.start", new { source = "ApiTester.McpServer" });
    }

    public void Emit(string eventName, object? fields = null, string level = "Information")
    {
        if (_writer is null)
            return;

        var data = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["level"] = level,
            ["event"] = eventName,
            ["correlationId"] = CorrelationId
        };

        if (fields is not null)
        {
            var serialized = JsonSerializer.Serialize(fields);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serialized)!;
            foreach (var property in parsed)
                data[property.Key] = SanitizeField(property.Key, property.Value);
        }

        var line = JsonSerializer.Serialize(data);
        lock (_gate)
        {
            _writer.WriteLine(line);
            Console.Error.WriteLine(line);
        }
    }

    public void Dispose()
    {
        if (_writer is null)
            return;

        Emit("server.shutdown");
        _writer.Dispose();
    }

    private static object? SanitizeField(string key, JsonElement value)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        if (normalized is "authorization" or "apikey" or "bearertoken" or "accesstoken" or "token" or "secret" or "password")
            return "[redacted]";

        if ((normalized == "url" || normalized == "source") && value.ValueKind == JsonValueKind.String)
            return SafeLocation(value.GetString());

        return value.Clone();
    }

    internal static string SafeLocation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath;
        }

        var fileName = Path.GetFileName(value);
        return string.IsNullOrWhiteSpace(fileName) ? "[local-source]" : fileName;
    }
}
