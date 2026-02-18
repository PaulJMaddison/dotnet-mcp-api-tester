using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Services;
using ApiTester.Web.Reports;
using ApiTester.Web.IntegrationTests;

namespace ApiTester.SecurityTests;

public sealed class TokenLeakageRegressionTests
{
    [Fact(DisplayName = "LEAK-01 authorization/token values are redacted from exports and evidence packs")]
    public void Leak01_RedactsTokensFromLogsArtifactsReportsAndEvidence()
    {
        const string token = "super-secret-token";

        var redaction = new RedactionService();
        var run = BuildRun(token);
        var patterns = new[] { "super-secret-token", "Bearer\\s+[^\\s]+", "token=\\w+" };
        var redacted = RunExportRedactor.RedactRun(run, redaction, patterns);

        var redactedSerialized = JsonSerializer.Serialize(redacted);
        Assert.DoesNotContain(token, redactedSerialized, StringComparison.Ordinal);

        var reportMd = RunReportGenerator.Generate(redacted, RunReportFormat.Markdown);
        var reportHtml = RunReportGenerator.Generate(redacted, RunReportFormat.Html);
        var runJson = RunExportGenerator.GenerateJson(redacted, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain(token, reportMd, StringComparison.Ordinal);
        Assert.DoesNotContain(token, reportHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(token, runJson, StringComparison.Ordinal);

        var evidenceBytes = EvidencePackBuilder.BuildZip(
            run,
            org: new OrganisationRecord(run.OrganisationId, "Security", "security", DateTime.UtcNow, redactionRules: patterns.ToList()),
            auditEvents: [],
            redactionService: redaction,
            jsonOptions: new JsonSerializerOptions(JsonSerializerDefaults.Web),
            createdUtc: DateTime.UtcNow,
            signingKey: null);

        using var archive = new ZipArchive(new MemoryStream(evidenceBytes), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var content = reader.ReadToEnd();
            Assert.DoesNotContain(token, content, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "LEAK-02 API errors never echo bearer token values")]
    public async Task Leak02_ProblemDetailsNeverEchoAuthorizationToken()
    {
        const string token = "super-secret-token";

        using var factory = new ApiTesterWebFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/projects/not-a-guid");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
    }

    private static TestRunRecord BuildRun(string token)
    {
        return new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            OrganisationId = Guid.NewGuid(),
            Actor = "security-tester",
            OwnerKey = "owner",
            ProjectKey = "proj",
            OperationId = "getStatus",
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow,
            Result = new TestRunResult
            {
                OperationId = "getStatus",
                TotalCases = 1,
                Passed = 1,
                Failed = 0,
                Blocked = 0,
                TotalDurationMs = 12,
                Results =
                [
                    new TestCaseResult
                    {
                        Name = "case",
                        Method = "GET",
                        Url = "https://api.example.test/status",
                        Pass = true,
                        StatusCode = 200,
                        DurationMs = 12,
                        ResponseSnippet = $"Authorization: Bearer {token}; token=abc123"
                    }
                ]
            }
        };
    }
}
