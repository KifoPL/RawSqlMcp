using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Services.Schema;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Services;

public class DatabaseProviderRegistryTests
{
    [Test]
    [Arguments("sqlserver", typeof(SqlClientFactory), typeof(SqlServerSchemaReader))]
    [Arguments("mssql", typeof(SqlClientFactory), typeof(SqlServerSchemaReader))]
    [Arguments("sqlite", typeof(SqliteFactory), typeof(SqliteSchemaReader))]
    [Arguments("sqlite3", typeof(SqliteFactory), typeof(SqliteSchemaReader))]
    [Arguments("postgres", typeof(NpgsqlFactory), typeof(PostgresSchemaReader))]
    [Arguments("postgresql", typeof(NpgsqlFactory), typeof(PostgresSchemaReader))]
    [Arguments("mysql", typeof(MySqlConnectorFactory), typeof(MySqlSchemaReader))]
    [Arguments("mariadb", typeof(MySqlConnectorFactory), typeof(MySqlSchemaReader))]
    public void Resolve_ReturnsFactoryAndSchemaReaderForAlias(string alias, Type expectedFactoryType, Type expectedSchemaReaderType)
    {
        DatabaseProvider provider = DatabaseProviderRegistry.CreateDefault().Resolve(alias);

        provider.Factory.ShouldBeOfType(expectedFactoryType);
        provider.SchemaReader.ShouldBeOfType(expectedSchemaReaderType);
    }

    [Test]
    public void Resolve_ThrowsForUnsupportedProvider()
    {
        DatabaseProviderRegistry registry = DatabaseProviderRegistry.CreateDefault();

        Should.Throw<ArgumentException>(() => registry.Resolve("oracle"))
              .Message.ShouldContain("Database provider 'oracle' is not supported. Supported providers: mariadb, mssql, mysql, postgres, postgresql, sqlite, sqlite3, sqlserver.");
    }
}