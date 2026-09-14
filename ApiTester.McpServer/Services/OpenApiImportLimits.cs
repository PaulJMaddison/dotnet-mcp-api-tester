namespace ApiTester.McpServer.Services;

public static class OpenApiImportLimits
{
    // Large enough for substantial bundled contracts such as GitHub's ~13 MB
    // description while still enforcing a hard resource boundary on imports.
    public const int MaxSpecBytes = 16 * 1024 * 1024;
}
