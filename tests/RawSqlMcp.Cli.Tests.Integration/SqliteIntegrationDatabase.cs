using RawSqlMcp.Cli.Tests.Integration.Helpers.Databases;

namespace RawSqlMcp.Cli.Tests.Integration;

public sealed class SqliteIntegrationDatabase : IIntegrationDatabase
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(),
                                                         $"raw-sql-mcp-{Guid.NewGuid():N}.db");

    public string Name => "Sqlite";

    public string Provider => "sqlite";

    public string ConnectionString => $"Data Source={_databasePath}";

    public Task StartAsync() => Task.CompletedTask;

    public async Task SeedAsync()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              create table widgets (id integer primary key, name text not null);
                              insert into widgets (id, name) values (1, 'alpha'), (2, 'beta');
                              """;
        await command.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync()
    {
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);

        return ValueTask.CompletedTask;
    }
}