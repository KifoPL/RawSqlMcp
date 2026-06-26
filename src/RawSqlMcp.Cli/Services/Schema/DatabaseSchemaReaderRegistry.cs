namespace RawSqlMcp.Cli.Services.Schema;

public sealed class DatabaseSchemaReaderRegistry(Dictionary<string, IDatabaseSchemaReader> readers)
{
    public static DatabaseSchemaReaderRegistry CreateDefault()
        => new(new(StringComparer.OrdinalIgnoreCase)
        {
            ["sqlserver"] = new SqlServerSchemaReader(),
            ["mssql"] = new SqlServerSchemaReader(),
            ["sqlite"] = new SqliteSchemaReader(),
            ["postgres"] = new PostgresSchemaReader(),
            ["postgresql"] = new PostgresSchemaReader(),
            ["mysql"] = new MySqlSchemaReader(),
            ["mariadb"] = new MySqlSchemaReader()
        });

    public IDatabaseSchemaReader Resolve(string providerName)
    {
        if (readers.TryGetValue(providerName,
                                out IDatabaseSchemaReader? reader))
            return reader;

        string supported = string.Join(", ",
                                       readers.Keys.OrderBy(name => name,
                                                            StringComparer.OrdinalIgnoreCase));
        throw new ArgumentException($"Database schema reader for provider '{providerName}' is not supported. Supported providers: {supported}.");
    }
}