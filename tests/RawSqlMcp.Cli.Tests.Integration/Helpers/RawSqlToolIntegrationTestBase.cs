using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Models.Dtos;
using RawSqlMcp.Cli.Tests.Integration.Helpers.Databases;
using RawSqlMcp.Cli.Tools;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Integration.Helpers;

public abstract class RawSqlToolIntegrationTestBase
{
    protected static async Task StartDatabaseAsync(IIntegrationDatabase database)
    {
        await database.StartAsync();
        await database.SeedAsync();
    }

    protected static async Task AssertExecuteQueryAsync(IIntegrationDatabase database)
    {
        RawSqlTool tool = CreateTool(database);

        string rows = await tool.ExecuteQueryAsync(database.Name,
                                                   "select id, name from widgets order by id");
        rows.ShouldBe("""[{"id":"1","name":"alpha"},{"id":"2","name":"beta"}]""",
                      database.Name);
    }

    protected static async Task AssertExecuteParametrizedQueryAsync(IIntegrationDatabase database)
    {
        RawSqlTool tool = CreateTool(database);

        string filteredRows = await tool.ExecuteParametrizedQueryAsync(
                                  database.Name,
                                  "select id, name from widgets where id = @id",
                                  new Dictionary<string, object?> { ["@id"] = 2 });
        filteredRows.ShouldBe("""[{"id":"2","name":"beta"}]""",
                              database.Name);
    }

    protected static async Task AssertExecuteScalarAsync(IIntegrationDatabase database)
    {
        RawSqlTool tool = CreateTool(database);

        string count = await tool.ExecuteScalarAsync(database.Name,
                                                     "select count(*) from widgets");
        count.ShouldBe("\"2\"",
                       database.Name);
    }

    protected static async Task AssertExecuteParametrizedScalarAsync(IIntegrationDatabase database)
    {
        RawSqlTool tool = CreateTool(database);

        string scalar = await tool.ExecuteParametrizedScalarAsync(
                            database.Name,
                            "select name from widgets where id = @id",
                            new Dictionary<string, object?> { ["@id"] = 1 });
        scalar.ShouldBe("\"alpha\"",
                        database.Name);
    }

    protected static async Task AssertCultureInvariantDecimalAsync(IIntegrationDatabase database)
    {
        RawSqlTool tool = CreateTool(database);
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
        try
        {
            string scalar = await tool.ExecuteScalarAsync(database.Name,
                                                          "select 1.5");
            scalar.ShouldBe("\"1.5\"",
                            database.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    protected static async Task AssertGetSchemaAsync(IIntegrationDatabase database)
    {
        RawSqlTool tool = CreateTool(database);

        DatabaseSchemaDto schema = await tool.GetSchemaAsync(database.Name);

        schema.DatabaseName.ShouldBe(database.Name);
        TableDto widgets = schema.Schemas.SelectMany(s => s.Tables).Single(t => t.Name == "widgets");
        widgets.Columns.Select(c => (c.Name, c.IsPrimaryKey, c.PrimaryKeyOrdinal))
               .ShouldBe([("id", true, 1), ("name", false, null)],
                         database.Name);
        TableDto parts = schema.Schemas.SelectMany(s => s.Tables).Single(t => t.Name == "widget_parts");
        parts.Columns.Select(c => (c.Name, c.IsPrimaryKey, c.PrimaryKeyOrdinal))
             .ShouldBe([("widget_id", true, 2), ("part_no", true, 1), ("label", false, null)],
                       database.Name);

        DatabaseSchemaDto filtered = await tool.GetSchemaAsync(database.Name,
                                                               parts.SchemaName.ToUpperInvariant());
        filtered.Schemas.Select(s => s.Name).ShouldBe([parts.SchemaName],
                                                      database.Name);
        (await tool.GetSchemaAsync(database.Name,
                                   "no_such_schema")).Schemas.ShouldBeEmpty(database.Name);
    }

    private static RawSqlTool CreateTool(IIntegrationDatabase database)
    {
        var options = Options.Create(new RawSqlOptions
        {
            Databases = new()
            {
                [database.Name] = new RawSqlDatabaseOptions
                {
                    Provider = database.Provider,
                    ConnectionString = database.ConnectionString
                }
            }
        });

        return new(options,
                   new DatabaseConnectionResolver(options),
                   DatabaseProviderRegistry.CreateDefault(),
                   NullLogger<RawSqlTool>.Instance);
    }
}