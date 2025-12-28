namespace ApiTester.McpServer.Options;

public sealed class PersistenceOptions
{
    public string Provider { get; set; } = "File";
    public string ConnectionString { get; set; } = "";
}
