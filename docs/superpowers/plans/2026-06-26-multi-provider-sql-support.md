# Multi Provider SQL Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make RawSqlMcp execute raw SQL against SQL Server, SQLite, PostgreSQL, MySQL, and MariaDB with provider-aware configuration and minimal custom provider code.

**Architecture:** Use ADO.NET `DbProviderFactory`/`DbProviderFactories` for the common execution path: resolve configured provider name, create `DbConnection`, create `DbCommand`, execute, serialize `DbDataReader`. Keep provider-specific code only for schema metadata, where SQL engines expose different catalog structures.

**Tech Stack:** .NET 10, ModelContextProtocol, `System.Data.Common`, `DbProviderFactory`, Microsoft.Data.SqlClient, Microsoft.Data.Sqlite, Npgsql, MySqlConnector, TUnit, Shouldly.

## Global Constraints

- Preserve existing MCP tool names and method signatures.
- Preserve legacy `RawSqlMcp__ConnectionStrings__Default=...`; treat legacy entries as SQL Server.
- Use this exact provider-aware option shape:

```csharp
namespace RawSqlMcp.Cli.Models.Options;

public sealed class RawSqlDatabaseOptions
{
    public required string Provider { get; set; }
    public required string ConnectionString { get; set; }
}
```

- Do not claim support for "any SQL dialect"; claim built-in support for `sqlserver`, `sqlite`, `postgres`, `postgresql`, `mysql`, and `mariadb`.
- Do not use EF Core for this feature. It adds model/change-tracking/query-translation concerns the MCP server does not need.
- Keep query safety semantics unchanged: the server executes user-provided SQL and does not block destructive statements.

---

## Current-State Findings

- `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:4` imports `Microsoft.Data.SqlClient`.
- `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:28` and `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:196` instantiate `SqlConnection` directly.
- `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:68`, `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:95`, `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:125`, and `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:151` use `SqlCommand`.
- `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:170` accepts `SqlDataReader`, so result serialization is SQL Server-specific.
- `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs:196` creates a connection inside an `await using` scope and returns a command referencing that connection after the helper exits. The refactor must keep connection, command, and reader lifetimes in the same method scope.

## File Structure

- Modify `src/RawSqlMcp.Cli/Models/Options/RawSqlOptions.cs`: add `Databases`.
- Create `src/RawSqlMcp.Cli/Models/Options/RawSqlDatabaseOptions.cs`: required `Provider` and `ConnectionString`.
- Create `src/RawSqlMcp.Cli/Services/DatabaseConnectionDefinition.cs`: resolved database name, provider, and connection string.
- Create `src/RawSqlMcp.Cli/Services/IDatabaseConnectionResolver.cs`: resolves configured names.
- Create `src/RawSqlMcp.Cli/Services/DatabaseConnectionResolver.cs`: merges `Databases` and legacy `ConnectionStrings`.
- Create `src/RawSqlMcp.Cli/Services/DatabaseProviderFactoryRegistry.cs`: normalizes aliases and returns `DbProviderFactory`.
- Create `src/RawSqlMcp.Cli/Services/Schema/IDatabaseSchemaReader.cs`: schema metadata boundary.
- Create `src/RawSqlMcp.Cli/Services/Schema/DatabaseSchemaReaderRegistry.cs`: maps provider aliases to schema readers.
- Create `src/RawSqlMcp.Cli/Services/Schema/SqlServerSchemaReader.cs`.
- Create `src/RawSqlMcp.Cli/Services/Schema/SqliteSchemaReader.cs`.
- Create `src/RawSqlMcp.Cli/Services/Schema/PostgresSchemaReader.cs`.
- Create `src/RawSqlMcp.Cli/Services/Schema/MySqlSchemaReader.cs`.
- Modify `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs`: execute through `DbProviderFactory`.
- Modify `src/RawSqlMcp.Cli/Tools/RawSqlStartupTool.cs`: use resolver for database names.
- Modify `src/RawSqlMcp.Cli/Program.cs`: register resolver, provider factory registry, and schema readers.
- Modify `Directory.Packages.props` and `src/RawSqlMcp.Cli/RawSqlMcp.Cli.csproj`: add database provider packages.
- Modify `README.md`, `src/RawSqlMcp.Cli/.mcp/server.json`, and packaging tests.

### Task 1: Provider-Aware Configuration

**Files:**
- Modify: `src/RawSqlMcp.Cli/Models/Options/RawSqlOptions.cs`
- Create: `src/RawSqlMcp.Cli/Models/Options/RawSqlDatabaseOptions.cs`
- Create: `src/RawSqlMcp.Cli/Services/DatabaseConnectionDefinition.cs`
- Create: `src/RawSqlMcp.Cli/Services/IDatabaseConnectionResolver.cs`
- Create: `src/RawSqlMcp.Cli/Services/DatabaseConnectionResolver.cs`
- Modify: `src/RawSqlMcp.Cli/Tools/RawSqlStartupTool.cs`
- Create: `tests/RawSqlMcp.Cli.Tests.Unit/Services/DatabaseConnectionResolverTests.cs`
- Modify: `tests/RawSqlMcp.Cli.Tests.Unit/Tools/RawSqlStartupToolTests.cs`

**Interfaces:**
- Produces: `RawSqlDatabaseOptions` with required `Provider` and `ConnectionString`.
- Produces: `DatabaseConnectionDefinition(string Name, string Provider, string ConnectionString)`.
- Produces: `IDatabaseConnectionResolver.Resolve(string databaseName)` and `IDatabaseConnectionResolver.ListNames()`.

- [ ] **Step 1: Write failing resolver tests**

```csharp
using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Services;

public class DatabaseConnectionResolverTests
{
    [Test]
    public void Resolve_ReturnsProviderAwareDatabase()
    {
        var options = Options.Create(new RawSqlOptions
        {
            Databases = new()
            {
                ["Local"] = new RawSqlDatabaseOptions
                {
                    Provider = "sqlite",
                    ConnectionString = "Data Source=:memory:"
                }
            }
        });

        var resolver = new DatabaseConnectionResolver(options);

        DatabaseConnectionDefinition result = resolver.Resolve("Local");

        result.Name.ShouldBe("Local");
        result.Provider.ShouldBe("sqlite");
        result.ConnectionString.ShouldBe("Data Source=:memory:");
    }

    [Test]
    public void Resolve_TreatsLegacyConnectionStringsAsSqlServer()
    {
        var options = Options.Create(new RawSqlOptions
        {
            ConnectionStrings = new()
            {
                ["Default"] = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
            }
        });

        var resolver = new DatabaseConnectionResolver(options);

        DatabaseConnectionDefinition result = resolver.Resolve("Default");

        result.Provider.ShouldBe("sqlserver");
        result.ConnectionString.ShouldContain("Server=localhost");
    }

    [Test]
    public void ListNames_ReturnsProviderAwareAndLegacyNamesWithoutDuplicates()
    {
        var options = Options.Create(new RawSqlOptions
        {
            ConnectionStrings = new() { ["Default"] = "Server=legacy" },
            Databases = new()
            {
                ["Default"] = new RawSqlDatabaseOptions
                {
                    Provider = "sqlite",
                    ConnectionString = "Data Source=:memory:"
                },
                ["Reporting"] = new RawSqlDatabaseOptions
                {
                    Provider = "postgres",
                    ConnectionString = "Host=localhost;Database=reporting"
                }
            }
        });

        var resolver = new DatabaseConnectionResolver(options);

        resolver.ListNames().ShouldBe(["Default", "Reporting"]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore --filter DatabaseConnectionResolverTests`

Expected: FAIL with compiler errors for missing resolver/configuration types.

- [ ] **Step 3: Add options and resolver implementation**

Create `src/RawSqlMcp.Cli/Models/Options/RawSqlDatabaseOptions.cs`:

```csharp
namespace RawSqlMcp.Cli.Models.Options;

public sealed class RawSqlDatabaseOptions
{
    public required string Provider { get; set; }
    public required string ConnectionString { get; set; }
}
```

Modify `src/RawSqlMcp.Cli/Models/Options/RawSqlOptions.cs`:

```csharp
namespace RawSqlMcp.Cli.Models.Options;

public class RawSqlOptions
{
    public const string Key = "RawSqlMcp";
    public const int DefaultCommandTimeout = 30;
    public Dictionary<string, string> ConnectionStrings { get; set; } = new();
    public Dictionary<string, RawSqlDatabaseOptions> Databases { get; set; } = new();
    public int? CommandTimeout { get; set; }
}
```

Create `src/RawSqlMcp.Cli/Services/DatabaseConnectionDefinition.cs`:

```csharp
namespace RawSqlMcp.Cli.Services;

public sealed record DatabaseConnectionDefinition(
    string Name,
    string Provider,
    string ConnectionString);
```

Create `src/RawSqlMcp.Cli/Services/IDatabaseConnectionResolver.cs`:

```csharp
namespace RawSqlMcp.Cli.Services;

public interface IDatabaseConnectionResolver
{
    DatabaseConnectionDefinition Resolve(string databaseName);
    string[] ListNames();
}
```

Create `src/RawSqlMcp.Cli/Services/DatabaseConnectionResolver.cs`:

```csharp
using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;

namespace RawSqlMcp.Cli.Services;

public sealed class DatabaseConnectionResolver(IOptions<RawSqlOptions> options) : IDatabaseConnectionResolver
{
    public DatabaseConnectionDefinition Resolve(string databaseName)
    {
        if (options.Value.Databases.TryGetValue(databaseName, out RawSqlDatabaseOptions? database))
            return new(databaseName, database.Provider, database.ConnectionString);

        if (options.Value.ConnectionStrings.TryGetValue(databaseName, out string? legacyConnectionString))
            return new(databaseName, "sqlserver", legacyConnectionString);

        throw new ArgumentException($"Database '{databaseName}' not found.");
    }

    public string[] ListNames()
        => options.Value.Databases.Keys
                  .Concat(options.Value.ConnectionStrings.Keys)
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .Order(StringComparer.OrdinalIgnoreCase)
                  .ToArray();
}
```

- [ ] **Step 4: Update startup tool to list resolver names**

Modify `src/RawSqlMcp.Cli/Tools/RawSqlStartupTool.cs`:

```csharp
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore --filter "DatabaseConnectionResolverTests|RawSqlStartupToolTests"`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RawSqlMcp.Cli/Models/Options src/RawSqlMcp.Cli/Services src/RawSqlMcp.Cli/Tools/RawSqlStartupTool.cs tests/RawSqlMcp.Cli.Tests.Unit
git commit -m "feat: add provider-aware database configuration"
```

### Task 2: Lightweight Provider Factory Registry

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/RawSqlMcp.Cli/RawSqlMcp.Cli.csproj`
- Create: `src/RawSqlMcp.Cli/Services/DatabaseProviderFactoryRegistry.cs`
- Modify: `src/RawSqlMcp.Cli/Program.cs`
- Create: `tests/RawSqlMcp.Cli.Tests.Unit/Services/DatabaseProviderFactoryRegistryTests.cs`

**Interfaces:**
- Produces: `DatabaseProviderFactoryRegistry.Resolve(string providerName): DbProviderFactory`.
- Provider aliases: `sqlserver`, `mssql`, `sqlite`, `postgres`, `postgresql`, `mysql`, `mariadb`.

- [ ] **Step 1: Write failing provider factory tests**

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using RawSqlMcp.Cli.Services;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Services;

public class DatabaseProviderFactoryRegistryTests
{
    [Test]
    [Arguments("sqlserver", typeof(SqlClientFactory))]
    [Arguments("mssql", typeof(SqlClientFactory))]
    [Arguments("sqlite", typeof(SqliteFactory))]
    [Arguments("postgres", typeof(NpgsqlFactory))]
    [Arguments("postgresql", typeof(NpgsqlFactory))]
    [Arguments("mysql", typeof(MySqlConnectorFactory))]
    [Arguments("mariadb", typeof(MySqlConnectorFactory))]
    public void Resolve_ReturnsFactoryForAlias(string alias, Type expectedType)
    {
        DatabaseProviderFactoryRegistry registry = DatabaseProviderFactoryRegistry.CreateDefault();

        registry.Resolve(alias).ShouldBeOfType(expectedType);
    }

    [Test]
    public void Resolve_ThrowsForUnsupportedProvider()
    {
        DatabaseProviderFactoryRegistry registry = DatabaseProviderFactoryRegistry.CreateDefault();

        Should.Throw<ArgumentException>(() => registry.Resolve("oracle"))
              .Message.ShouldContain("Database provider 'oracle' is not supported.");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore --filter DatabaseProviderFactoryRegistryTests`

Expected: FAIL with missing package/type errors.

- [ ] **Step 3: Add provider package references**

Modify `Directory.Packages.props`:

```xml
<PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.9" />
<PackageVersion Include="MySqlConnector" Version="2.6.1" />
<PackageVersion Include="Npgsql" Version="10.0.3" />
```

Modify `src/RawSqlMcp.Cli/RawSqlMcp.Cli.csproj`:

```xml
<PackageReference Include="Microsoft.Data.Sqlite"/>
<PackageReference Include="MySqlConnector"/>
<PackageReference Include="Npgsql"/>
```

- [ ] **Step 4: Add provider factory registry**

Create `src/RawSqlMcp.Cli/Services/DatabaseProviderFactoryRegistry.cs`:

```csharp
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace RawSqlMcp.Cli.Services;

public sealed class DatabaseProviderFactoryRegistry(Dictionary<string, DbProviderFactory> factories)
{
    public static DatabaseProviderFactoryRegistry CreateDefault()
        => new(new(StringComparer.OrdinalIgnoreCase)
        {
            ["sqlserver"] = SqlClientFactory.Instance,
            ["mssql"] = SqlClientFactory.Instance,
            ["sqlite"] = SqliteFactory.Instance,
            ["postgres"] = NpgsqlFactory.Instance,
            ["postgresql"] = NpgsqlFactory.Instance,
            ["mysql"] = MySqlConnectorFactory.Instance,
            ["mariadb"] = MySqlConnectorFactory.Instance
        });

    public DbProviderFactory Resolve(string providerName)
    {
        if (factories.TryGetValue(providerName, out DbProviderFactory? factory))
            return factory;

        string supported = string.Join(", ", factories.Keys.Order(StringComparer.OrdinalIgnoreCase));
        throw new ArgumentException($"Database provider '{providerName}' is not supported. Supported providers: {supported}.");
    }
}
```

- [ ] **Step 5: Register the factory registry**

Modify `src/RawSqlMcp.Cli/Program.cs`:

```csharp
builder.Services.AddSingleton(DatabaseProviderFactoryRegistry.CreateDefault());
builder.Services.AddSingleton<IDatabaseConnectionResolver, DatabaseConnectionResolver>();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore --filter DatabaseProviderFactoryRegistryTests`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props src/RawSqlMcp.Cli tests/RawSqlMcp.Cli.Tests.Unit
git commit -m "feat: add database provider factory registry"
```

### Task 3: Common Raw SQL Execution Through DbProviderFactory

**Files:**
- Modify: `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs`
- Create: `tests/RawSqlMcp.Cli.Tests.Unit/Tools/RawSqlToolSqliteTests.cs`

**Interfaces:**
- Consumes: `IDatabaseConnectionResolver` and `DatabaseProviderFactoryRegistry`.
- Produces: query/scalar execution via `DbConnection`, `DbCommand`, and `DbDataReader`.

- [ ] **Step 1: Write failing SQLite execution tests**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Tools;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Tools;

public class RawSqlToolSqliteTests
{
    [Test]
    public async Task ExecuteQueryAsync_ReturnsRowsForSqlite()
    {
        string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}";
        await SeedSqliteAsync(connectionString);
        RawSqlTool tool = CreateTool(connectionString);

        string result = await tool.ExecuteQueryAsync("Local", "select id, name from widgets order by id");

        result.ShouldBe("""[{"id":"1","name":"alpha"},{"id":"2","name":"beta"}]""");
    }

    [Test]
    public async Task ExecuteParametrizedScalarAsync_ReturnsValueForSqlite()
    {
        string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}";
        await SeedSqliteAsync(connectionString);
        RawSqlTool tool = CreateTool(connectionString);

        string result = await tool.ExecuteParametrizedScalarAsync(
            "Local",
            "select name from widgets where id = @id",
            new Dictionary<string, object?> { ["@id"] = 2 });

        result.ShouldBe("beta");
    }

    private static RawSqlTool CreateTool(string connectionString)
    {
        var options = Options.Create(new RawSqlOptions
        {
            Databases = new()
            {
                ["Local"] = new RawSqlDatabaseOptions
                {
                    Provider = "sqlite",
                    ConnectionString = connectionString
                }
            }
        });

        return new RawSqlTool(
            options,
            new DatabaseConnectionResolver(options),
            DatabaseProviderFactoryRegistry.CreateDefault(),
            NullLogger<RawSqlTool>.Instance);
    }

    private static async Task SeedSqliteAsync(string connectionString)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table widgets (id integer primary key, name text not null);
            insert into widgets (name) values ('alpha'), ('beta');
            """;
        await command.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore --filter RawSqlToolSqliteTests`

Expected: FAIL because `RawSqlTool` still depends on `SqlCommand`.

- [ ] **Step 3: Refactor RawSqlTool execution methods**

Modify `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs` so query and scalar methods follow this pattern:

```csharp
using System.Data.Common;

private async Task<DbConnection> OpenConnectionAsync(string databaseName, CancellationToken cancellationToken)
{
    DatabaseConnectionDefinition definition = connectionResolver.Resolve(databaseName);
    DbProviderFactory factory = providerFactoryRegistry.Resolve(definition.Provider);
    DbConnection connection = factory.CreateConnection()
        ?? throw new InvalidOperationException($"Provider '{definition.Provider}' did not create a connection.");
    connection.ConnectionString = definition.ConnectionString;
    await connection.OpenAsync(cancellationToken);
    return connection;
}

private DbCommand CreateCommand(DbConnection connection, string sqlQuery)
{
    DbCommand command = connection.CreateCommand();
    command.CommandText = sqlQuery;
    command.CommandTimeout = options.Value.CommandTimeout ?? RawSqlOptions.DefaultCommandTimeout;
    return command;
}

private static void AddParameters(DbCommand command, Dictionary<string, object?> parameters)
{
    foreach ((string name, object? value) in parameters)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
```

Each public method should keep the connection, command, and reader in the same scope:

```csharp
await using DbConnection connection = await OpenConnectionAsync(databaseName, cancellationToken);
await using DbCommand command = CreateCommand(connection, sqlQuery);
await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
return await JsonArray(reader, cancellationToken);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore --filter RawSqlToolSqliteTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RawSqlMcp.Cli/Tools/RawSqlTool.cs tests/RawSqlMcp.Cli.Tests.Unit/Tools/RawSqlToolSqliteTests.cs
git commit -m "feat: execute raw SQL through provider factories"
```

### Task 4: Minimal Schema Strategy Layer

**Files:**
- Create: `src/RawSqlMcp.Cli/Services/Schema/IDatabaseSchemaReader.cs`
- Create: `src/RawSqlMcp.Cli/Services/Schema/DatabaseSchemaReaderRegistry.cs`
- Create: `src/RawSqlMcp.Cli/Services/Schema/SqlServerSchemaReader.cs`
- Create: `src/RawSqlMcp.Cli/Services/Schema/SqliteSchemaReader.cs`
- Create: `src/RawSqlMcp.Cli/Services/Schema/PostgresSchemaReader.cs`
- Create: `src/RawSqlMcp.Cli/Services/Schema/MySqlSchemaReader.cs`
- Modify: `src/RawSqlMcp.Cli/Tools/RawSqlTool.cs`
- Create: `tests/RawSqlMcp.Cli.Tests.Unit/Tools/RawSqlToolSqliteSchemaTests.cs`

**Interfaces:**
- Produces: `IDatabaseSchemaReader.ReadSchemaAsync(DbConnection connection, string databaseName, CancellationToken cancellationToken): Task<DatabaseSchemaDto>`.
- Keeps custom code limited to schema discovery, not connection/command creation.

- [ ] **Step 1: Add SQLite schema test**

```csharp
[Test]
public async Task GetSchemaAsync_ReturnsSqliteSchema()
{
    string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db")}";
    await SeedSqliteAsync(connectionString);
    RawSqlTool tool = CreateTool(connectionString);

    var schema = await tool.GetSchemaAsync("Local");

    schema.DatabaseName.ShouldBe("Local");
    schema.Schemas.Single().Name.ShouldBe("main");
    schema.Schemas.Single().Tables.Single(table => table.Name == "widgets")
          .Columns.Select(column => column.Name)
          .ShouldBe(["id", "name"]);
}
```

- [ ] **Step 2: Implement schema reader registry and SQLite reader first**

Create the registry:

```csharp
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

    public IDatabaseSchemaReader Resolve(string providerName) => readers[providerName];
}
```

Implement `SqliteSchemaReader` with `sqlite_master` and `pragma_table_xinfo`; implement other readers with `INFORMATION_SCHEMA` in separate commits if needed.

- [ ] **Step 3: Wire `GetSchemaAsync` through the schema reader registry**

`GetSchemaAsync` should resolve the database, open the provider factory connection, resolve a schema reader by provider name, call it, and then apply the existing optional `schemaName` filter.

- [ ] **Step 4: Run tests**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore --filter "RawSqlToolSqliteTests|RawSqlToolSqliteSchemaTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RawSqlMcp.Cli/Services/Schema src/RawSqlMcp.Cli/Tools/RawSqlTool.cs tests/RawSqlMcp.Cli.Tests.Unit
git commit -m "feat: add schema readers for provider metadata"
```

### Task 5: Documentation and Package Metadata

**Files:**
- Modify: `README.md`
- Modify: `src/RawSqlMcp.Cli/.mcp/server.json`
- Modify: `tests/RawSqlMcp.Cli.Tests.Unit/Packaging/ServerJsonTests.cs`

**Interfaces:**
- Documents `RawSqlMcp__Databases__Name__Provider` and `RawSqlMcp__Databases__Name__ConnectionString`.
- Documents legacy SQL Server connection string compatibility.

- [ ] **Step 1: Update README examples**

Use provider-aware examples for SQLite, PostgreSQL, MySQL/MariaDB, and SQL Server. Keep a short note that legacy `RawSqlMcp__ConnectionStrings__Default` still works as SQL Server.

- [ ] **Step 2: Update MCP metadata**

Change the MCP description to mention SQL Server, SQLite, PostgreSQL, MySQL, and MariaDB. Add environment variables for:

```json
"RawSqlMcp__Databases__Default__Provider"
```

and:

```json
"RawSqlMcp__Databases__Default__ConnectionString"
```

- [ ] **Step 3: Run tests**

Run: `dotnet test --solution RawSqlMcp.slnx --no-restore`

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add README.md src/RawSqlMcp.Cli/.mcp/server.json tests/RawSqlMcp.Cli.Tests.Unit/Packaging/ServerJsonTests.cs
git commit -m "docs: document multi-provider configuration"
```

## Self-Review

- Spec coverage: The plan now uses the requested `required` option shape and replaces the custom connection provider layer with `DbProviderFactory`.
- Placeholder scan: No placeholder tasks are intentionally left open; provider schema readers are scoped to the metadata layer.
- Type consistency: `DatabaseProviderFactoryRegistry`, `DatabaseConnectionResolver`, and `IDatabaseSchemaReader` are introduced before use.
- Risk: Result execution becomes lightweight and provider-neutral; schema discovery remains the only area requiring dialect-specific code because engines expose catalog metadata differently.
