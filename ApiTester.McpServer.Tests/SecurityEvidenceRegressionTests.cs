using ApiTester.McpServer.Rag;
using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class SecurityEvidenceRegressionTests
{
    [Fact]
    public void SecurityEvidenceOnlyEmitsLocationForApiKeySchemes()
    {
        var document = Parse("""
        {
          "openapi":"3.0.1",
          "info":{"title":"Security","version":"1"},
          "paths":{},
          "components":{"securitySchemes":{
            "oauth":{"type":"oauth2","flows":{"clientCredentials":{"tokenUrl":"https://example.test/token","scopes":{"read":"Read"}}}},
            "apiKey":{"type":"apiKey","name":"X-Api-Key","in":"header"},
            "bearer":{"type":"http","scheme":"bearer"}
          }}
        }
        """);

        var chunks = new OpenApiEvidenceBuilder().Build(
            document,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Security",
            "1",
            DateTime.UtcNow);

        var oauth = Assert.Single(chunks, c => c.Metadata.TryGetValue("SecurityScheme", out var value) && value == "oauth");
        var apiKey = Assert.Single(chunks, c => c.Metadata.TryGetValue("SecurityScheme", out var value) && value == "apiKey");
        var bearer = Assert.Single(chunks, c => c.Metadata.TryGetValue("SecurityScheme", out var value) && value == "bearer");

        Assert.Contains("TYPE: OAuth2", oauth.Text);
        Assert.DoesNotContain("IN:", oauth.Text);

        Assert.Contains("TYPE: ApiKey", apiKey.Text);
        Assert.Contains("NAME: X-Api-Key", apiKey.Text);
        Assert.Contains("IN: Header", apiKey.Text);

        Assert.Contains("TYPE: Http", bearer.Text);
        Assert.Contains("SCHEME: bearer", bearer.Text);
        Assert.DoesNotContain("IN:", bearer.Text);
    }

    private static Microsoft.OpenApi.Models.OpenApiDocument Parse(string text)
    {
        var document = new OpenApiStringReader().Read(text, out var diagnostics);
        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);
        OpenApiSecuritySemantics.PreserveExplicitOverrides(document, text);
        return document;
    }
}
