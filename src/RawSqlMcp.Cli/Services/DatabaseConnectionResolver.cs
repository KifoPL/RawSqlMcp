using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;

namespace RawSqlMcp.Cli.Services;

public sealed class DatabaseConnectionResolver(IOptions<RawSqlOptions> options) : IDatabaseConnectionResolver
{
    public DatabaseConnectionDefinition Resolve(string databaseName)
    {
        if (options.Value.Databases.TryGetValue(databaseName,
                                                out RawSqlDatabaseOptions? database))
            return new(databaseName,
                       database.Provider,
                       database.ConnectionString);

#pragma warning disable CS0618
        if (options.Value.ConnectionStrings.TryGetValue(databaseName,
                                                        out string? legacyConnectionString))
#pragma warning restore CS0618
            return new(databaseName,
                       "sqlserver",
                       legacyConnectionString);

        throw new ArgumentException($"Database '{databaseName}' not found.");
    }

    public string[] ListNames()
    {
#pragma warning disable CS0618
        IEnumerable<string> connectionStringNames = options.Value.ConnectionStrings.Keys;
#pragma warning restore CS0618

        return options.Value.Databases.Keys
                      .Concat(connectionStringNames)
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderBy(name => name,
                               StringComparer.OrdinalIgnoreCase)
                      .ToArray();
    }
}