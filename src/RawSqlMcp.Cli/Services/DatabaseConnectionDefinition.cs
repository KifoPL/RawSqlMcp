namespace RawSqlMcp.Cli.Services;

public sealed record DatabaseConnectionDefinition(
    string Name,
    string Provider,
    string ConnectionString);