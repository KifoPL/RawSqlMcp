using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace RawSqlMcp.Cli.Services;

public sealed class DatabaseProviderFactoryRegistry(Dictionary<string, DbProviderFactory> factories)
{
    public static DatabaseProviderFactoryRegistry CreateDefault()
        => new(new(StringComparer.OrdinalIgnoreCase)
        {
            ["sqlserver"] = SqlClientFactory.Instance,
            ["mssql"] = SqlClientFactory.Instance,
            ["sqlite"] = SqliteFactory.Instance,
            ["sqlite3"] = SqliteFactory.Instance,
            ["postgres"] = NpgsqlFactory.Instance,
            ["postgresql"] = NpgsqlFactory.Instance,
            ["mysql"] = MySqlConnectorFactory.Instance,
            ["mariadb"] = MySqlConnectorFactory.Instance
        });

    public DbProviderFactory Resolve(string providerName)
    {
        if (factories.TryGetValue(providerName,
                                  out DbProviderFactory? factory))
            return factory;

        string supported = string.Join(", ",
                                       factories.Keys.OrderBy(name => name,
                                                              StringComparer.OrdinalIgnoreCase));
        throw new ArgumentException($"Database provider '{providerName}' is not supported. Supported providers: {supported}.");
    }
}