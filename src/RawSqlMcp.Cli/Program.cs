using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Tools;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions => { consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace; });

var jsonOptions = RawSqlMcp.Cli.Models.RawSqlJsonContext.Default.Options;

builder.Services.AddOptions<RawSqlOptions>().BindConfiguration(RawSqlOptions.Key);

builder.Services.AddMcpServer()
       .WithStdioServerTransport()
       .WithTools<RawSqlStartupTool>(jsonOptions)
       .WithTools<RawSqlTool>(jsonOptions);

await builder.Build().RunAsync();