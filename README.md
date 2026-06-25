# Raw SQL MCP

<!-- mcp-name: io.github.kifopl/RawSqlMcp -->

Raw SQL MCP is a STDIO MCP server for executing raw SQL against SQL Server.

Use it carefully. The server does not protect you from destructive queries, expensive queries, SQL injection, missing pagination, or unsafe data exposure. Always review and approve queries before running them.

![Install and run demo](docs/vhs/install-run.gif)

## Installation

Run the server with `dnx`:

```bash
dnx RawSqlMcp
```

For a specific version:

```bash
dnx RawSqlMcp@1.0.0
```

## Configuration

Connection strings are configured with environment variables under `RawSqlMcp__ConnectionStrings__`.

Configure the default SQL Server connection string:

```bash
export RawSqlMcp__ConnectionStrings__Default="Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
```

Add more named databases by changing the final segment:

```bash
export RawSqlMcp__ConnectionStrings__Reporting="Server=localhost,1433;Database=Reporting;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
```

Optional command timeout, in seconds:

```bash
export RawSqlMcp__CommandTimeout=30
```

## Usage

Register the server in an MCP client as a STDIO server:

```json
{
  "servers": {
    "raw-sql-mcp": {
      "command": "dnx",
      "args": ["RawSqlMcp"],
      "env": {
        "RawSqlMcp__ConnectionStrings__Default": "Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
      }
    }
  }
}
```

The server exposes tools for listing configured database names, reading SQL Server schema metadata, and executing raw, parameterized, and scalar SQL queries.
