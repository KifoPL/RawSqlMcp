using System.ComponentModel;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RawSqlMcp.Cli.Models;
using RawSqlMcp.Cli.Models.Dtos;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;

namespace RawSqlMcp.Cli.Tools;

[McpServerToolType]
public class RawSqlTool(
    IOptions<RawSqlOptions> options,
    IDatabaseConnectionResolver connectionResolver,
    DatabaseProviderRegistry providerRegistry,
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
            await using DbConnection connection = await OpenConnectionAsync(definition,
                                                                            cancellationToken);

            return await providerRegistry.Resolve(definition.Provider)
                                         .SchemaReader
                                         .ReadSchemaAsync(connection,
                                                          definition.Name,
                                                          string.IsNullOrWhiteSpace(schemaName) ? null : schemaName,
                                                          cancellationToken);
        }
        catch (Exception e) when (e is not McpException and not OperationCanceledException)
        {
            logger.LogError(e,
                            "Error retrieving schema for database '{DatabaseName}'",
                            databaseName);
            throw new McpException(e.Message,
                                   e);
        }
    }

    [McpServerTool, Description("Executes a raw SQL query against the specified database.")]
    public Task<string> ExecuteQueryAsync(
        [Description($"Name of the connection string excluding the '{RawSqlOptions.Key}' prefix")] string databaseName,
        [Description("SQL query to execute (does not check for injection, write operations, pagination, etc.)")]
        string sqlQuery,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(databaseName,
                        sqlQuery,
                        null,
                        command => JsonArray(command,
                                             cancellationToken),
                        cancellationToken);

    [McpServerTool, Description("Executes a parameterized SQL query against the specified database.")]
    public Task<string> ExecuteParametrizedQueryAsync(
        [Description($"Name of the connection string excluding the '{RawSqlOptions.Key}' prefix")] string databaseName,
        [Description(
            "SQL query with parameters to execute (does not check for injection, write operations, pagination, etc.)")]
        string sqlQuery,
        [Description("Dictionary of parameter names and values")] Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(databaseName,
                        sqlQuery,
                        parameters,
                        command => JsonArray(command,
                                             cancellationToken),
                        cancellationToken);

    [McpServerTool,
     Description(
         "Executes a SQL query against the specified database and returns the first column of the first row as a JSON value (null for SQL NULL).")]
    public Task<string> ExecuteScalarAsync(
        [Description("The name of the database to execute the query against")] string databaseName,
        [Description("The SQL query to execute")] string sqlQuery,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(databaseName,
                        sqlQuery,
                        null,
                        command => JsonScalar(command,
                                              cancellationToken),
                        cancellationToken);

    [McpServerTool,
     Description(
         "Executes a parameterized SQL query against the specified database and returns the first column of the first row as a JSON value (null for SQL NULL).")]
    public Task<string> ExecuteParametrizedScalarAsync(
        [Description("The name of the database to execute the query against")] string databaseName,
        [Description("The SQL query to execute")] string sqlQuery,
        [Description("Dictionary of parameter names and values")] Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(databaseName,
                        sqlQuery,
                        parameters,
                        command => JsonScalar(command,
                                              cancellationToken),
                        cancellationToken);

    private async Task<string> ExecuteAsync(string databaseName,
                                            string sqlQuery,
                                            Dictionary<string, object?>? parameters,
                                            Func<DbCommand, Task<string>> execute,
                                            CancellationToken cancellationToken)
    {
        try
        {
            DatabaseConnectionDefinition definition = connectionResolver.Resolve(databaseName);
            await using DbConnection connection = await OpenConnectionAsync(definition,
                                                                            cancellationToken);
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sqlQuery;
            command.CommandTimeout = options.Value.CommandTimeout ?? RawSqlOptions.DefaultCommandTimeout;
            if (parameters is not null)
                AddParameters(command,
                              parameters);

            return await execute(command);
        }
        catch (Exception e) when (e is not McpException and not OperationCanceledException)
        {
            logger.LogError(e,
                            "Error executing SQL query against database '{DatabaseName}'",
                            databaseName);
            throw new McpException(e.Message,
                                   e);
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(DatabaseConnectionDefinition definition,
                                                         CancellationToken cancellationToken)
    {
        DbProviderFactory factory = providerRegistry.Resolve(definition.Provider).Factory;
        DbConnection connection = factory.CreateConnection()
                                  ?? throw new InvalidOperationException($"Provider '{definition.Provider}' did not create a connection.");

        connection.ConnectionString = definition.ConnectionString;
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddParameters(DbCommand command,
                                      Dictionary<string, object?> parameters)
    {
        foreach ((string name, object? value) in parameters)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            // MCP arguments arrive as JsonElement, which no ADO.NET driver accepts.
            parameter.Value = value is JsonElement json
                                  ? FromJson(json)
                                  : value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static object FromJson(JsonElement json)
        => json.ValueKind switch
        {
            JsonValueKind.String => json.GetString()!,
            JsonValueKind.Number when json.TryGetInt64(out long integer) => integer,
            JsonValueKind.Number when json.TryGetDecimal(out decimal number) => number,
            JsonValueKind.Number => json.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => DBNull.Value,
            _ => json.GetRawText()
        };

    private static string? Format(object? value)
        => value switch
        {
            null or DBNull => null,
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTime dateTime => dateTime.ToString("O",
                                                   CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O",
                                                                     CultureInfo.InvariantCulture),
            _ => Convert.ToString(value,
                                  CultureInfo.InvariantCulture)
        };

    private static async Task<string> JsonScalar(DbCommand command,
                                                 CancellationToken cancellationToken)
        => JsonSerializer.Serialize(Format(await command.ExecuteScalarAsync(cancellationToken)),
                                    RawSqlJsonContext.Default.String);

    private static async Task<string> JsonArray(DbCommand command,
                                                CancellationToken cancellationToken)
    {
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        HashSet<string> usedNames = new(StringComparer.Ordinal);
        string[] names = new string[reader.FieldCount];
        for (int i = 0; i < names.Length; i++)
        {
            string name = reader.GetName(i);
            string n = name;
            for (int k = 1; !usedNames.Add(n); k++)
                n = $"{name}_{i}" + (k > 1 ? $"_{k}" : "");
            names[i] = n;
        }

        using var stream = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            while (await reader.ReadAsync(cancellationToken))
            {
                writer.WriteStartObject();
                for (int i = 0; i < names.Length; i++)
                {
                    string? value = Format(reader.GetValue(i));
                    if (value is null)
                        writer.WriteNull(names[i]);
                    else
                        writer.WriteString(names[i],
                                           value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}