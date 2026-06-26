namespace RawSqlMcp.Cli.Models.Options;

public class RawSqlOptions
{
    public const string Key = "RawSqlMcp";
    public const int DefaultCommandTimeout = 30;

    [Obsolete("Use RawSqlMcp__Databases__{Name}__Provider and RawSqlMcp__Databases__{Name}__ConnectionString instead. Legacy connection strings are interpreted as SQL Server.")]
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();

    public Dictionary<string, RawSqlDatabaseOptions> Databases { get; set; } = new();
    public int? CommandTimeout { get; set; }
}