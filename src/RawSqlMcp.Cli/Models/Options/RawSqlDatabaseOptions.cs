using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace RawSqlMcp.Cli.Models.Options;

public sealed class RawSqlDatabaseOptions
{
    [Description("The database provider. For example, 'SqlServer', 'PostgreSql', 'MySql', etc.")]
    public string Provider { get; set; } = null!;

    [Description("The database connection string.")]
    public string ConnectionString { get; set; } = null!;
}