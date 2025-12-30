using System.ComponentModel;
using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class PersistenceTools
{
    private readonly ITestRunStore _store;
    private readonly IOptions<PersistenceOptions> _opts;

    public PersistenceTools(ITestRunStore store, IOptions<PersistenceOptions> opts)
    {
        _store = store;
        _opts = opts;
    }

    [McpServerTool, Description("Show which persistence provider is active and which store implementation is being used.")]
    public object ApiPersistenceStatus()
    {
        var p = _opts.Value;

        return new
        {
            provider = (p.Provider ?? "File").Trim(),
            hasConnectionString = !string.IsNullOrWhiteSpace(p.ConnectionString),
            storeImplementation = _store.GetType().FullName
        };
    }
}
