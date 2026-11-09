using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using RawSqlMcp.Cli.Models;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
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
    public async Task ExecuteQueryAsync_EscapesNonAsciiLikeBefore()
    {
        RawSqlTool tool = CreateTool($"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}");

        string result = await tool.ExecuteQueryAsync("Local",
                                                     "select 'zażółć' as word");

        result.ShouldBe("""[{"word":"za\u017C\u00F3\u0142\u0107"}]""");
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

        result.ShouldBe("\"beta\"");
    }

    [Test]
    public async Task ExecuteScalarAsync_ReturnsJsonNullForSqlNull()
    {
        RawSqlTool tool = CreateTool($"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}");

        (await tool.ExecuteScalarAsync("Local",
                                       "select null")).ShouldBe("null");
        (await tool.ExecuteScalarAsync("Local",
                                       "select 'null'")).ShouldBe("\"null\"");
    }

    [Test]
    public async Task ExecuteParametrizedQueryAsync_AcceptsJsonElementParameters()
    {
        string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}";
        await SeedSqliteAsync(connectionString);
        RawSqlTool tool = CreateTool(connectionString);
        Dictionary<string, object?> parameters = JsonSerializer.Deserialize(
                                                     """{"@id":2,"@name":"beta","@flag":true,"@ratio":1.5,"@missing":null}""",
                                                     RawSqlJsonContext.Default.DictionaryStringObject)!;

        string result = await tool.ExecuteParametrizedQueryAsync(
                            "Local",
                            "select id, @flag as flag, @ratio as ratio, @missing as missing from widgets where id = @id and name = @name",
                            parameters);

        result.ShouldBe("""[{"id":"2","flag":"1","ratio":"1.5","missing":null}]""");
    }

    [Test]
    public async Task ExecuteQueryAsync_ThrowsMcpExceptionWithDriverMessage()
    {
        RawSqlTool tool = CreateTool($"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}");

        McpException exception = await Should.ThrowAsync<McpException>(() => tool.ExecuteQueryAsync("Local",
                                                                                                    "select * from missing_table"));

        exception.Message.ShouldContain("no such table");
    }

    [Test]
    public async Task ExecuteQueryAsync_KeepsDuplicateColumnNames()
    {
        RawSqlTool tool = CreateTool($"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}");

        string result = await tool.ExecuteQueryAsync("Local",
                                                     "select 1 as x, 2 as x");

        result.ShouldBe("""[{"x":"1","x_1":"2"}]""");
    }

    [Test]
    public async Task ExecuteQueryAsync_RenamesDuplicatesWithoutCollidingWithExistingNames()
    {
        RawSqlTool tool = CreateTool($"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}");

        string result = await tool.ExecuteQueryAsync("Local",
                                                     "select 1 as x_2, 2 as x, 3 as x");

        using JsonDocument document = JsonDocument.Parse(result);
        var row = document.RootElement[0].EnumerateObject().ToList();
        row.Select(p => p.Name).Distinct().Count().ShouldBe(3);
        row.Select(p => p.Value.GetString()).ShouldBe(["1", "2", "3"], ignoreOrder: true);
    }

    [Test]
    public async Task ExecuteQueryAsync_FormatsValuesIndependentOfCulture()
    {
        RawSqlTool tool = CreateTool($"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}");
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
        try
        {
            string rows = await tool.ExecuteQueryAsync("Local",
                                                       "select 1.5 as ratio, x'0102' as bytes");
            string scalar = await tool.ExecuteScalarAsync("Local",
                                                          "select 1.5");

            rows.ShouldBe("""[{"ratio":"1.5","bytes":"AQI="}]""");
            scalar.ShouldBe("\"1.5\"");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
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
                   DatabaseProviderRegistry.CreateDefault(),
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