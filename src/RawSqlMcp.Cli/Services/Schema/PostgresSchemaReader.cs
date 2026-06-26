using System.Data.Common;
using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Services.Schema;

public sealed class PostgresSchemaReader : IDatabaseSchemaReader
{
    public async Task<DatabaseSchemaDto> ReadSchemaAsync(
        DbConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            select
                current_database() as "DatabaseName",
                c.table_schema as "SchemaName",
                c.table_name as "TableName",
                t.table_type as "TableType",
                c.column_name as "ColumnName",
                c.data_type as "DataType",
                (c.is_nullable = 'YES') as "IsNullable",
                (tc.constraint_type = 'PRIMARY KEY') as "IsPrimaryKey",
                c.ordinal_position as "Ordinal",
                c.character_maximum_length as "MaxLength",
                c.character_octet_length as "OctetLength",
                c.numeric_precision as "NumericPrecision",
                c.numeric_scale as "NumericScale",
                c.datetime_precision as "DateTimePrecision",
                c.column_default as "DefaultValue",
                kcu.ordinal_position as "PrimaryKeyOrdinal"
            from information_schema.columns c
            join information_schema.tables t
              on t.table_schema = c.table_schema and t.table_name = c.table_name
            left join information_schema.key_column_usage kcu
              on kcu.table_schema = c.table_schema and kcu.table_name = c.table_name and kcu.column_name = c.column_name
            left join information_schema.table_constraints tc
              on tc.constraint_schema = kcu.constraint_schema and tc.constraint_name = kcu.constraint_name and tc.constraint_type = 'PRIMARY KEY'
            where c.table_schema not in ('pg_catalog', 'information_schema')
            order by c.table_schema, c.table_name, c.ordinal_position;
            """;

        return DatabaseSchemaBuilder.Build(connection.Database,
                                           await SchemaColumnReader.ReadRowsAsync(command,
                                                                                  cancellationToken));
    }
}