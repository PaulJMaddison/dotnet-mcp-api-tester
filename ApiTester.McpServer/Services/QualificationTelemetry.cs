using System.Text.Json;

namespace ApiTester.McpServer.Services;

public sealed class QualificationTelemetry : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");

    public QualificationTelemetry()
    {
        var directory = Environment.GetEnvironmentVariable("API_TESTER_QUALIFICATION_DIR")
            ?? Path.Combine("qualification", "final-run");
        Directory.CreateDirectory(directory);
        _writer = new StreamWriter(Path.Combine(directory, "telemetry.ndjson"), append: true) { AutoFlush = true };
        Emit("server.start", new { source = "ApiTester.McpServer" });
    }

    public void Emit(string eventName, object? fields = null, string level = "Information")
    {
        var data = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["level"] = level,
            ["event"] = eventName,
            ["correlationId"] = CorrelationId
        };
        if (fields is not null)
            foreach (var property in JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(fields))!)
                data[property.Key] = property.Value;
        var line = JsonSerializer.Serialize(data);
        lock (_gate)
        {
            _writer.WriteLine(line);
            Console.Error.WriteLine(line);
        }
    }

    public void Dispose()
    {
        Emit("server.shutdown");
        _writer.Dispose();
    }
}
