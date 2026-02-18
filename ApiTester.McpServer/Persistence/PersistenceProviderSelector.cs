using ApiTester.McpServer.Options;

namespace ApiTester.McpServer.Persistence;

public enum PersistenceProvider
{
    File,
    SqlServer,
    PostgreSql
}

public sealed record PersistenceSelection(PersistenceProvider Provider, string ConnectionString)
{
    public bool UseSqlProvider => Provider is PersistenceProvider.SqlServer or PersistenceProvider.PostgreSql
        && !string.IsNullOrWhiteSpace(ConnectionString);
}

public static class PersistenceProviderSelector
{
    public static PersistenceSelection Select(PersistenceOptions options)
    {
        if (options is null)
            return new PersistenceSelection(PersistenceProvider.File, string.Empty);

        var provider = (options.Provider ?? "File").Trim();
        var connectionString = (options.ConnectionString ?? string.Empty).Trim();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(connectionString)
                ? new PersistenceSelection(PersistenceProvider.File, string.Empty)
                : new PersistenceSelection(PersistenceProvider.SqlServer, connectionString);

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(connectionString)
                ? new PersistenceSelection(PersistenceProvider.File, string.Empty)
                : new PersistenceSelection(PersistenceProvider.PostgreSql, connectionString);

        return new PersistenceSelection(PersistenceProvider.File, string.Empty);
    }
}
