using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using RawSqlMcp.Cli.Models.Options;

namespace RawSqlMcp.Cli.Tools;

[McpServerToolType]
public class RawSqlStartupTool(IOptions<RawSqlOptions> options)
{
    [McpServerTool, Description("Returns the list of available databases.")]
    public string[] AvailableDatabases() => options.Value.ConnectionStrings.Keys.ToArray();
}