using System.Data.Common;
using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Services.Schema;

public sealed class SqlServerSchemaReader : IDatabaseSchemaReader
{
    public async Task<DatabaseSchemaDto> ReadSchemaAsync(
        DbConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            select
                db_name() as DatabaseName,
                c.TABLE_SCHEMA as SchemaName,
                c.TABLE_NAME as TableName,
                t.TABLE_TYPE as TableType,
                c.COLUMN_NAME as ColumnName,
                c.DATA_TYPE as DataType,
                case c.IS_NULLABLE when 'YES' then cast(1 as bit) else cast(0 as bit) end as IsNullable,
                case when tc.CONSTRAINT_TYPE = 'PRIMARY KEY' then cast(1 as bit) else cast(0 as bit) end as IsPrimaryKey,
                c.ORDINAL_POSITION as Ordinal,
                c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
                c.CHARACTER_OCTET_LENGTH as OctetLength,
                c.NUMERIC_PRECISION as NumericPrecision,
                c.NUMERIC_SCALE as NumericScale,
                c.DATETIME_PRECISION as DateTimePrecision,
                c.COLUMN_DEFAULT as DefaultValue,
                kcu.ORDINAL_POSITION as PrimaryKeyOrdinal
            from INFORMATION_SCHEMA.COLUMNS c
            join INFORMATION_SCHEMA.TABLES t
              on t.TABLE_SCHEMA = c.TABLE_SCHEMA and t.TABLE_NAME = c.TABLE_NAME
            left join INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              on kcu.TABLE_SCHEMA = c.TABLE_SCHEMA and kcu.TABLE_NAME = c.TABLE_NAME and kcu.COLUMN_NAME = c.COLUMN_NAME
            left join INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
              on tc.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA and tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME and tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            order by c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;
            """;

        return DatabaseSchemaBuilder.Build(connection.Database,
                                           await SchemaColumnReader.ReadRowsAsync(command,
                                                                                  cancellationToken));
    }
}