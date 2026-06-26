namespace RawSqlMcp.Cli.Services;

public interface IDatabaseConnectionResolver
{
    DatabaseConnectionDefinition Resolve(string databaseName);
    string[] ListNames();
}