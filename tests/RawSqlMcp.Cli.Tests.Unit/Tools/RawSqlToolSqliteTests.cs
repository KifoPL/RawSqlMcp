using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Services.Schema;
using RawSqlMcp.Cli.Tools;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Tools;

public class RawSqlToolSqliteTests
{
    [Test]
    public async Task ExecuteQueryAsync_ReturnsRowsForSqlite()
    {
        string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}";
        await SeedSqliteAsync(connectionString);
        RawSqlTool tool = CreateTool(connectionString);

        string result = await tool.ExecuteQueryAsync("Local",
                                                     "select id, name from widgets order by id");

        result.ShouldBe("""[{"id":"1","name":"alpha"},{"id":"2","name":"beta"}]""");
    }

    [Test]
    public async Task ExecuteParametrizedScalarAsync_ReturnsValueForSqlite()
    {
        string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}";
        await SeedSqliteAsync(connectionString);
        RawSqlTool tool = CreateTool(connectionString);

        string result = await tool.ExecuteParametrizedScalarAsync(
            "Local",
            "select name from widgets where id = @id",
            new Dictionary<string, object?> { ["@id"] = 2 });

        result.ShouldBe("beta");
    }

    [Test]
    public async Task GetSchemaAsync_ReturnsSqliteSchema()
    {
        string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}";
        await SeedSqliteAsync(connectionString);
        RawSqlTool tool = CreateTool(connectionString);

        var schema = await tool.GetSchemaAsync("Local");

        schema.DatabaseName.ShouldBe("Local");
        schema.Schemas.Single().Name.ShouldBe("main");
        var table = schema.Schemas.Single().Tables.Single(t => t.Name == "widgets");
        table.Columns.Select(column => column.Name).ShouldBe(["id", "name"]);
        table.Columns.Single(column => column.Name == "id").IsPrimaryKey.ShouldBe(true);
    }

    private static RawSqlTool CreateTool(string connectionString)
    {
        var options = Options.Create(new RawSqlOptions
        {
            Databases = new()
            {
                ["Local"] = new RawSqlDatabaseOptions
                {
                    Provider = "sqlite",
                    ConnectionString = connectionString
                }
            }
        });

        return new(options,
                   new DatabaseConnectionResolver(options),
                   DatabaseProviderFactoryRegistry.CreateDefault(),
                   DatabaseSchemaReaderRegistry.CreateDefault(),
                   NullLogger<RawSqlTool>.Instance);
    }

    private static async Task SeedSqliteAsync(string connectionString)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table widgets (id integer primary key, name text not null);
            insert into widgets (name) values ('alpha'), ('beta');
            """;
        await command.ExecuteNonQueryAsync();
    }
}