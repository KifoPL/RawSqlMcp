using RawSqlMcp.Cli.Tests.Integration.Helpers;
using RawSqlMcp.Cli.Tests.Integration.Helpers.Databases;

namespace RawSqlMcp.Cli.Tests.Integration;

[Category("Integration")]
public sealed class RawSqlToolPostgreSqlIntegrationTests : RawSqlToolIntegrationTestBase
{
    private static readonly PostgreSqlIntegrationDatabase Database = new();

    [Before(Class)]
    public static Task StartDatabaseAsync() => StartDatabaseAsync(Database);

    [After(Class)]
    public static ValueTask StopDatabaseAsync() => Database.DisposeAsync();

    [Test]
    public Task RawSqlTool_ExecutesQueries() => AssertExecuteQueryAsync(Database);

    [Test]
    public Task RawSqlTool_ExecutesParametrizedQueries() => AssertExecuteParametrizedQueryAsync(Database);

    [Test]
    public Task RawSqlTool_ExecutesScalar() => AssertExecuteScalarAsync(Database);

    [Test]
    public Task RawSqlTool_ExecutesQueriesAndReadsSchema() => AssertExecuteParametrizedScalarAsync(Database);
}