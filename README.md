# Raw SQL MCP

[![CI](https://github.com/KifoPL/RawSqlMcp/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/KifoPL/RawSqlMcp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/RawSqlMcp?label=NuGet)](https://www.nuget.org/packages/RawSqlMcp)
[![Latest release](https://img.shields.io/github/v/tag/KifoPL/RawSqlMcp?label=release)](https://github.com/KifoPL/RawSqlMcp/tags)

<!-- mcp-name: io.github.KifoPL/RawSqlMcp -->

Raw SQL MCP is a STDIO MCP server for executing raw SQL against SQL Server, SQLite, PostgreSQL, MySQL, and MariaDB.

> [!IMPORTANT]
> Use it carefully. The server does not protect you from destructive queries, expensive queries, SQL injection, missing pagination, or unsafe data exposure. Always review and approve queries before running them.

![Install and run demo](docs/vhs/install-run.gif)

## Installation

Run the server with `dnx`:

```bash
dnx RawSqlMcp
```

For a specific version:

```bash
dnx RawSqlMcp@0.0.2
```

Package: [RawSqlMcp on NuGet](https://www.nuget.org/packages/RawSqlMcp)

## Configuration

Databases are configured with environment variables under `RawSqlMcp__Databases__`.

SQLite:

```bash
export RawSqlMcp__Databases__Local__Provider="sqlite"
export RawSqlMcp__Databases__Local__ConnectionString="Data Source=/absolute/path/app.db"
```

PostgreSQL:

```bash
export RawSqlMcp__Databases__Reporting__Provider="postgres"
export RawSqlMcp__Databases__Reporting__ConnectionString="Host=localhost;Port=5432;Database=reporting;Username=postgres;Password=postgres"
```

MySQL/MariaDB:

```bash
export RawSqlMcp__Databases__Shop__Provider="mysql"
export RawSqlMcp__Databases__Shop__ConnectionString="Server=localhost;Port=3306;Database=shop;User ID=mysql;Password=mysql"
```

SQL Server:

```bash
export RawSqlMcp__Databases__Default__Provider="sqlserver"
export RawSqlMcp__Databases__Default__ConnectionString="Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
```

Optional command timeout, in seconds:

```bash
export RawSqlMcp__CommandTimeout=30
```

> [!INFO]
> The legacy `RawSqlMcp__ConnectionStrings__Default="..."` format is obsolete. It still works for compatibility and is interpreted as SQL Server.

## Usage

Register the server in an MCP client as a STDIO server:

```json
{
  "servers": {
    "raw-sql-mcp": {
      "command": "dnx",
      "args": ["RawSqlMcp"],
      "env": {
        "RawSqlMcp__Databases__Default__Provider": "sqlserver",
        "RawSqlMcp__Databases__Default__ConnectionString": "Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
      }
    }
  }
}
```

The server exposes tools for listing configured database names, reading schema metadata, and executing raw, parameterized, and scalar SQL queries.

![Short end-to-end demo](docs/vhs/end-to-end.gif)
