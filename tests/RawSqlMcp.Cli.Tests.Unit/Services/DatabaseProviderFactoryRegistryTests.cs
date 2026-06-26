using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using RawSqlMcp.Cli.Services;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Services;

public class DatabaseProviderFactoryRegistryTests
{
    [Test]
    [Arguments("sqlserver", typeof(SqlClientFactory))]
    [Arguments("mssql", typeof(SqlClientFactory))]
    [Arguments("sqlite", typeof(SqliteFactory))]
    [Arguments("sqlite3", typeof(SqliteFactory))]
    [Arguments("postgres", typeof(NpgsqlFactory))]
    [Arguments("postgresql", typeof(NpgsqlFactory))]
    [Arguments("mysql", typeof(MySqlConnectorFactory))]
    [Arguments("mariadb", typeof(MySqlConnectorFactory))]
    public void Resolve_ReturnsFactoryForAlias(string alias, Type expectedType)
    {
        DatabaseProviderFactoryRegistry registry = DatabaseProviderFactoryRegistry.CreateDefault();

        registry.Resolve(alias).ShouldBeOfType(expectedType);
    }

    [Test]
    public void Resolve_ThrowsForUnsupportedProvider()
    {
        DatabaseProviderFactoryRegistry registry = DatabaseProviderFactoryRegistry.CreateDefault();

        Should.Throw<ArgumentException>(() => registry.Resolve("oracle"))
              .Message.ShouldContain("Database provider 'oracle' is not supported.");
    }
}