using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using RawSqlMcp.Cli.Services.Schema;

namespace RawSqlMcp.Cli.Services;

public sealed record DatabaseProvider(DbProviderFactory Factory, IDatabaseSchemaReader SchemaReader);

public sealed class DatabaseProviderRegistry(Dictionary<string, DatabaseProvider> providers)
{
    public static DatabaseProviderRegistry CreateDefault()
    {
        DatabaseProvider sqlServer = new(SqlClientFactory.Instance,
                                         new SqlServerSchemaReader());
        DatabaseProvider sqlite = new(SqliteFactory.Instance,
                                      new SqliteSchemaReader());
        DatabaseProvider postgres = new(NpgsqlFactory.Instance,
                                        new PostgresSchemaReader());
        DatabaseProvider mySql = new(MySqlConnectorFactory.Instance,
                                     new MySqlSchemaReader());

        return new(new(StringComparer.OrdinalIgnoreCase)
        {
            ["sqlserver"] = sqlServer,
            ["mssql"] = sqlServer,
            ["sqlite"] = sqlite,
            ["sqlite3"] = sqlite,
            ["postgres"] = postgres,
            ["postgresql"] = postgres,
            ["mysql"] = mySql,
            ["mariadb"] = mySql
        });
    }

    public DatabaseProvider Resolve(string providerName)
    {
        if (providers.TryGetValue(providerName,
                                  out DatabaseProvider? provider))
            return provider;

        string supported = string.Join(", ",
                                       providers.Keys.OrderBy(name => name,
                                                              StringComparer.OrdinalIgnoreCase));
        throw new ArgumentException($"Database provider '{providerName}' is not supported. Supported providers: {supported}.");
    }
}