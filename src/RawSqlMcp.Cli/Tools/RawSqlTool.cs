using System.ComponentModel;
using System.Data;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using RawSqlMcp.Cli.Models.Dtos;
using RawSqlMcp.Cli.Models.Options;

namespace RawSqlMcp.Cli.Tools;

[McpServerToolType]
public class RawSqlTool(IOptions<RawSqlOptions> options, ILogger<RawSqlTool> logger)
{
    [McpServerTool, Description("Returns the schema of the specified database.")]
    public async Task<DatabaseSchemaDto> GetSchemaAsync(
        [Description("Name of the database to retrieve the schema for")] string databaseName,
        [Description("Name of the schema to retrieve the DB schema for, if null you will get all schemas")] string? schemaName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!options.Value.ConnectionStrings.TryGetValue(databaseName,
                                                             out string? connectionString))
                throw new ArgumentException($"Database '{databaseName}' not found.");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            DataTable columns = await connection.GetSchemaAsync("Columns",
                                                                cancellationToken);
            columns.TableName = connection.Database;

            DatabaseSchemaDto schema = columns;

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
            await using SqlCommand command = await CreateSqlCommandAsync(databaseName,
                                                                         sqlQuery,
                                                                         cancellationToken);

            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            return await JsonArray(reader);
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
            await using SqlCommand command = await CreateSqlCommandAsync(databaseName,
                                                                         sqlQuery,
                                                                         cancellationToken);

            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key,
                                                param.Value ?? DBNull.Value);

            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            return await JsonArray(reader);
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
         "Executes a parameterized SQL query against the specified database and returns the first column of the first row.")]
    public async Task<string> ExecuteScalarAsync(
        [Description("The name of the database to execute the query against")] string databaseName,
        [Description("The SQL query to execute")] string sqlQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using SqlCommand command = await CreateSqlCommandAsync(databaseName,
                                                                         sqlQuery,
                                                                         cancellationToken);

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
            await using SqlCommand command = await CreateSqlCommandAsync(databaseName,
                                                                         sqlQuery,
                                                                         cancellationToken);

            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key,
                                                param.Value ?? DBNull.Value);

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

    private static async Task<string> JsonArray(SqlDataReader reader)
    {
        List<string> results = [];
        while (await reader.ReadAsync())
        {
            var row = new JsonObject();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i)?.ToString();

            results.Add(row.ToJsonString());
        }

        return $"[{string.Join(',', results)}]";
    }

    private async Task<SqlCommand> CreateSqlCommandAsync(string databaseName,
                                                         string sqlQuery,
                                                         CancellationToken cancellationToken)
    {
        SqlCommand? command = null;
        try
        {
            if (!options.Value.ConnectionStrings.TryGetValue(databaseName,
                                                             out string? connectionString))
                throw new ArgumentException($"Database '{databaseName}' not found.");

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            command = new(sqlQuery,
                          connection)
            {
                CommandTimeout = options.Value.CommandTimeout ?? RawSqlOptions.DefaultCommandTimeout
            };

            return command;
        }
        catch
        {
            if (command != null) await command.DisposeAsync();
            throw;
        }
    }
}