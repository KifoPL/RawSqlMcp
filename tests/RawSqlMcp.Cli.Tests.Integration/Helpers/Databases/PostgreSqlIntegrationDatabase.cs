using Testcontainers.PostgreSql;

namespace RawSqlMcp.Cli.Tests.Integration.Helpers.Databases;

public sealed class PostgreSqlIntegrationDatabase : IIntegrationDatabase
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
       .Build();

    public string Name => "PostgreSql";

    public string Provider => "postgres";

    public string ConnectionString => _container.GetConnectionString();

    public Task StartAsync() => _container.StartAsync();

    public Task SeedAsync()
        => ExecuteNonQueryAsync("""
                                create table widgets (id integer primary key, name text not null);
                                insert into widgets (id, name) values (1, 'alpha'), (2, 'beta');
                                """);

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}