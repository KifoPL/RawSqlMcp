namespace RawSqlMcp.Cli.Models.Options;

public class RawSqlOptions
{
    public const string Key = "RawSqlMcp";
    public const int DefaultCommandTimeout = 30;
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();
    public int? CommandTimeout { get; set; }
}