using System.ComponentModel;
using System.Data.Common;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using RawSqlMcp.Cli.Models.Dtos;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Services.Schema;

namespace RawSqlMcp.Cli.Tools;

[McpServerToolType]
public class RawSqlTool(
    IOptions<RawSqlOptions> options,
    IDatabaseConnectionResolver connectionResolver,
    DatabaseProviderFactoryRegistry providerFactoryRegistry,
    DatabaseSchemaReaderRegistry schemaReaderRegistry,
    ILogger<RawSqlTool> logger)
{
    [McpServerTool, Description("Returns the schema of the specified database.")]
    public async Task<DatabaseSchemaDto> GetSchemaAsync(
        [Description("Name of the database to retrieve the schema for")] string databaseName,
        [Description("Name of the schema to retrieve the DB schema for, if null you will get all schemas")] string? schemaName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DatabaseConnectionDefinition definition = connectionResolver.Resolve(databaseName);
            IDatabaseSchemaReader schemaReader = schemaReaderRegistry.Resolve(definition.Provider);
            await using DbConnection connection = await OpenConnectionAsync(definition,
                                                                            cancellationToken);

            DatabaseSchemaDto schema = await schemaReader.ReadSchemaAsync(connection,
                                                                          definition.Name,
                                                                          cancellationToken);

            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                schema = schema with
                {
                    Schemas = schema.Schemas.Where(s => string.Equals(s.Name,
                                                                      schemaName,
                                                                      StringComparison.OrdinalIgnoreCase))
                                    .ToArray()
                };
            }

            return schema;
        }
        catch (Exception e)
        {
            logger.LogError(e,
                            "Error retrieving schema for database '{DatabaseName}'",
                            databaseName);
            throw;
        }
    }

    [McpServerTool, Description("Executes a raw SQL query against the specified database.")]
    public async Task<string> ExecuteQueryAsync(
        [Description($"Name of the connection string excluding the '{RawSqlOptions.Key}' prefix")] string databaseName,
        [Description("SQL query to execute (does not check for injection, write operations, pagination, etc.)")]
        string sqlQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DatabaseConnectionDefinition definition = connectionResolver.Resolve(databaseName);
            await using DbConnection connection = await OpenConnectionAsync(definition,
                                                                            cancellationToken);
            await using DbCommand command = CreateCommand(connection,
                                                          sqlQuery);
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            return await JsonArray(reader,
                                   cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                            "Error executing raw SQL query");
            throw;
        }
    }

    [McpServerTool, Description("Executes a parameterized SQL query against the specified database.")]
    public async Task<string> ExecuteParametrizedQueryAsync(
        [Description($"Name of the connection string excluding the '{RawSqlOptions.Key}' prefix")] string databaseName,
        [Description(
            "SQL query with parameters to execute (does not check for injection, write operations, pagination, etc.)")]
        string sqlQuery,
        [Description("Dictionary of parameter names and values")] Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DatabaseConnectionDefinition definition = connectionResolver.Resolve(databaseName);
            await using DbConnection connection = await OpenConnectionAsync(definition,
                                                                            cancellationToken);
            await using DbCommand command = CreateCommand(connection,
                                                          sqlQuery);
            AddParameters(command,
                          parameters);
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            return await JsonArray(reader,
                                   cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                            "Error executing parameterized SQL query");
            throw;
        }
    }

    [McpServerTool,
     Description(
         "Executes a SQL query against the specified database and returns the first column of the first row.")]
    public async Task<string> ExecuteScalarAsync(
        [Description("The name of the database to execute the query against")] string databaseName,
        [Description("The SQL query to execute")] string sqlQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DatabaseConnectionDefinition definition = connectionResolver.Resolve(databaseName);
            await using DbConnection connection = await OpenConnectionAsync(definition,
                                                                            cancellationToken);
            await using DbCommand command = CreateCommand(connection,
                                                          sqlQuery);

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result?.ToString() ?? string.Empty;
        }
        catch (Exception e)
        {
            logger.LogError(e,
                            "Error executing scalar SQL query");
            throw;
        }
    }

    [McpServerTool,
     Description(
         "Executes a parameterized SQL query against the specified database and returns the first column of the first row.")]
    public async Task<string> ExecuteParametrizedScalarAsync(
        [Description("The name of the database to execute the query against")] string databaseName,
        [Description("The SQL query to execute")] string sqlQuery,
        [Description("Dictionary of parameter names and values")] Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DatabaseConnectionDefinition definition = connectionResolver.Resolve(databaseName);
            await using DbConnection connection = await OpenConnectionAsync(definition,
                                                                            cancellationToken);
            await using DbCommand command = CreateCommand(connection,
                                                          sqlQuery);
            AddParameters(command,
                          parameters);

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result?.ToString() ?? string.Empty;
        }
        catch (Exception e)
        {
            logger.LogError(e,
                            "Error executing parameterized scalar SQL query");
            throw;
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(DatabaseConnectionDefinition definition,
                                                         CancellationToken cancellationToken)
    {
        DbProviderFactory factory = providerFactoryRegistry.Resolve(definition.Provider);
        DbConnection connection = factory.CreateConnection()
                                  ?? throw new InvalidOperationException($"Provider '{definition.Provider}' did not create a connection.");

        connection.ConnectionString = definition.ConnectionString;
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private DbCommand CreateCommand(DbConnection connection,
                                    string sqlQuery)
    {
        DbCommand command = connection.CreateCommand();
        command.CommandText = sqlQuery;
        command.CommandTimeout = options.Value.CommandTimeout ?? RawSqlOptions.DefaultCommandTimeout;
        return command;
    }

    private static void AddParameters(DbCommand command,
                                      Dictionary<string, object?> parameters)
    {
        foreach ((string name, object? value) in parameters)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static async Task<string> JsonArray(DbDataReader reader,
                                                CancellationToken cancellationToken)
    {
        List<string> results = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new JsonObject();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i,
                                                                     cancellationToken)
                                             ? null
                                             : reader.GetValue(i)?.ToString();

            results.Add(row.ToJsonString());
        }

        return $"[{string.Join(',', results)}]";
    }
}