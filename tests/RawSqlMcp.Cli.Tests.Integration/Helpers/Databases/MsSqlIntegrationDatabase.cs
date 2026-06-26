using DotNet.Testcontainers.Builders;
using Testcontainers.MsSql;

namespace RawSqlMcp.Cli.Tests.Integration.Helpers.Databases;

public sealed class MsSqlIntegrationDatabase : IIntegrationDatabase
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
                                                .WithWaitStrategy(Wait.ForUnixContainer()
                                                                      .UntilMessageIsLogged("SQL Server is now ready for client connections",
                                                                           wait => wait.WithTimeout(TimeSpan.FromMinutes(3))))
                                                .Build();

    public string Name => "SqlServer";

    public string Provider => "sqlserver";

    public string ConnectionString => _container.GetConnectionString();

    public Task StartAsync() => _container.StartAsync();

    public Task SeedAsync()
        => ExecuteNonQueryAsync("""
                                create table widgets (id int not null primary key, name nvarchar(100) not null);
                                insert into widgets (id, name) values (1, N'alpha'), (2, N'beta');
                                """);

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private async Task ExecuteNonQueryAsync(string commandText)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}