using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Services.Schema;
using RawSqlMcp.Cli.Tools;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions => { consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace; });

var jsonOptions = RawSqlMcp.Cli.Models.RawSqlJsonContext.Default.Options;

builder.Services.AddOptions<RawSqlOptions>().BindConfiguration(RawSqlOptions.Key)
       .Validate(options =>
       {
#pragma warning disable CS0618 // Type or member is obsolete
           foreach (var (name, connectionString) in options.ConnectionStrings)
#pragma warning restore CS0618 // Type or member is obsolete
           {
               if (string.IsNullOrWhiteSpace(connectionString))
               {
                   throw new InvalidOperationException($"Database '{name}' is missing a connection string. Specify it using the configuration key '{RawSqlOptions.Key}__ConnectionStrings__{name}'. Also, consider migrating to the new configuration keys '{RawSqlOptions.Key}__Databases__{name}__Provider' and '{RawSqlOptions.Key}__Databases__{name}__ConnectionString'.");
               }
           }

           foreach (var (name, database) in options.Databases)
           {
               if (string.IsNullOrWhiteSpace(database.Provider))
               {
                   throw new InvalidOperationException($"Database '{name}' is missing a provider. Specify it using the configuration key '{RawSqlOptions.Key}__Databases__{name}__Provider'.");
               }
               if (string.IsNullOrWhiteSpace(database.ConnectionString))
               {
                   throw new InvalidOperationException($"Database '{name}' is missing a connection string. Specify it using the configuration key '{RawSqlOptions.Key}__Databases__{name}__ConnectionString'.");
               }
           }
           return true;
       }, "All databases must have a provider and connection string.")
      .ValidateOnStart();
builder.Services.AddSingleton<IDatabaseConnectionResolver, DatabaseConnectionResolver>();
builder.Services.AddSingleton(DatabaseProviderFactoryRegistry.CreateDefault());
builder.Services.AddSingleton(DatabaseSchemaReaderRegistry.CreateDefault());

builder.Services.AddMcpServer()
       .WithStdioServerTransport()
       .WithTools<RawSqlStartupTool>(jsonOptions)
       .WithTools<RawSqlTool>(jsonOptions);

await builder.Build().RunAsync();