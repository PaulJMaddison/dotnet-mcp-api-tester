using System.ComponentModel;
using ApiTester.McpServer.Options;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace ApiTester.McpServer.Tools;

[McpServerToolType]
public sealed class PersistenceTools
{
    private readonly ITestRunStore _store;
    private readonly IProjectStore _projects;
    private readonly IOptions<PersistenceOptions> _opts;

    public PersistenceTools(ITestRunStore store, IProjectStore projects, IOptions<PersistenceOptions> opts)
    {
        _store = store;
        _projects = projects;
        _opts = opts;
    }

    [McpServerTool, Description("Show which persistence provider is active and which store implementation is being used.")]
    public object ApiPersistenceStatus()
    {
        var p = _opts.Value;
        var selection = PersistenceProviderSelector.Select(p);

        return new
        {
            provider = selection.Provider.ToString(),
            hasConnectionString = !string.IsNullOrWhiteSpace(p.ConnectionString),
            testRunStore = _store.GetType().FullName,
            projectStore = _projects.GetType().FullName
        };
    }
}
