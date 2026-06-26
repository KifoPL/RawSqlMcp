using System.ComponentModel;
using ModelContextProtocol.Server;
using RawSqlMcp.Cli.Services;

namespace RawSqlMcp.Cli.Tools;

[McpServerToolType]
public class RawSqlStartupTool(IDatabaseConnectionResolver connectionResolver)
{
    [McpServerTool, Description("Returns the list of available databases.")]
    public string[] AvailableDatabases() => connectionResolver.ListNames();
}