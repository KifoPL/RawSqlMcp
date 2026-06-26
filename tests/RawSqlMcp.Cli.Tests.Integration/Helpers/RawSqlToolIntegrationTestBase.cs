using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Services.Schema;
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
        count.ShouldBe("2",
                       database.Name);
    }

    protected static async Task AssertExecuteParametrizedScalarAsync(IIntegrationDatabase database)
    {
        RawSqlTool tool = CreateTool(database);

        string scalar = await tool.ExecuteParametrizedScalarAsync(
                            database.Name,
                            "select name from widgets where id = @id",
                            new Dictionary<string, object?> { ["@id"] = 1 });
        scalar.ShouldBe("alpha",
                        database.Name);
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
                   DatabaseProviderFactoryRegistry.CreateDefault(),
                   DatabaseSchemaReaderRegistry.CreateDefault(),
                   NullLogger<RawSqlTool>.Instance);
    }
}