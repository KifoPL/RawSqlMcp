namespace RawSqlMcp.Cli.Tests.Integration.Helpers.Databases;

public interface IIntegrationDatabase : IAsyncDisposable
{
    string Name { get; }

    string Provider { get; }

    string ConnectionString { get; }

    Task StartAsync();

    Task SeedAsync();
}