using ApiTester.McpServer.Services;

namespace ApiTester.McpServer.Tests;

public sealed class QualificationTelemetryTests
{
    [Fact]
    public void TelemetryIsDisabledUnlessQualificationDirectoryIsExplicitlyConfigured()
    {
        using var telemetry = new QualificationTelemetry(null);

        Assert.False(telemetry.Enabled);
        telemetry.Emit("test.event", new { url = "https://example.test/path?secret=value" });
    }

    [Fact]
    public void TelemetryRedactsSecretsAndStripsQueryValuesFromLocations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "api-tester-telemetry-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var telemetry = new QualificationTelemetry(directory))
            {
                Assert.True(telemetry.Enabled);
                telemetry.Emit("test.event", new
                {
                    url = "https://example.test/orders?api_key=super-secret&customer=42",
                    source = "https://example.test/openapi.json?sig=signed-secret",
                    apiKey = "super-secret",
                    bearerToken = "bearer-secret"
                });
            }

            var text = File.ReadAllText(Path.Combine(directory, "telemetry.ndjson"));
            Assert.Contains("https://example.test/orders", text);
            Assert.Contains("https://example.test/openapi.json", text);
            Assert.DoesNotContain("super-secret", text);
            Assert.DoesNotContain("signed-secret", text);
            Assert.DoesNotContain("bearer-secret", text);
            Assert.DoesNotContain("customer=42", text);
            Assert.Contains("[redacted]", text);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
