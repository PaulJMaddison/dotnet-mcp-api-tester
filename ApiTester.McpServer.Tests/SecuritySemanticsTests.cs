using ApiTester.McpServer.Rag;
using Microsoft.OpenApi.Readers;

namespace ApiTester.McpServer.Tests;

public sealed class SecuritySemanticsTests
{
    [Fact]
    public void EmptyOperationSecurityOverridesGlobalSecurityInSemanticEvidence()
    {
        var document = new OpenApiStringReader().Read("""
        {
          "openapi":"3.0.1","info":{"title":"Security","version":"1"},
          "security":[{"bearer":[]}],
          "paths":{
            "/public":{"get":{"operationId":"publicGet","security":[],"responses":{"200":{"description":"OK"}}}},
            "/secure":{"get":{"operationId":"secureGet","responses":{"200":{"description":"OK"}}}}
          },
          "components":{"securitySchemes":{"bearer":{"type":"http","scheme":"bearer"}}}
        }
        """, out var diagnostics);

        Assert.NotNull(document);
        Assert.Empty(diagnostics.Errors);

        var chunks = new OpenApiEvidenceBuilder().Build(document, Guid.NewGuid(), Guid.NewGuid(), "Security", "1", DateTime.UtcNow);
        var publicEvidence = Assert.Single(chunks.Where(c => c.Metadata.TryGetValue("OperationId", out var id) && id == "publicGet"));
        var secureEvidence = Assert.Single(chunks.Where(c => c.Metadata.TryGetValue("OperationId", out var id) && id == "secureGet"));

        Assert.Contains("SECURITY: none documented", publicEvidence.Text);
        Assert.Contains("SECURITY: bearer", secureEvidence.Text);
    }
}
