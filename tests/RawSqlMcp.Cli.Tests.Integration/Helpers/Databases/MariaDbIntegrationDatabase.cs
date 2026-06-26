using Testcontainers.MariaDb;

namespace RawSqlMcp.Cli.Tests.Integration.Helpers.Databases;

public sealed class MariaDbIntegrationDatabase : IIntegrationDatabase
{
    private readonly MariaDbContainer _container = new MariaDbBuilder()
       .Build();

    public string Name => "MariaDb";

    public string Provider => "mariadb";

    public string ConnectionString => _container.GetConnectionString();

    public Task StartAsync() => _container.StartAsync();

    public Task SeedAsync()
        => ExecuteNonQueryAsync("""
                                create table widgets (id int not null primary key, name varchar(100) not null);
                                insert into widgets (id, name) values (1, 'alpha'), (2, 'beta');
                                """);

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using var connection = new MySqlConnector.MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}