using System.Data.Common;
using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Services.Schema;

public sealed class MySqlSchemaReader : IDatabaseSchemaReader
{
    public async Task<DatabaseSchemaDto> ReadSchemaAsync(
        DbConnection connection,
        string databaseName,
        string? schemaName,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            select
                c.TABLE_SCHEMA as SchemaName,
                c.TABLE_NAME as TableName,
                t.TABLE_TYPE as TableType,
                c.COLUMN_NAME as ColumnName,
                c.DATA_TYPE as DataType,
                c.IS_NULLABLE = 'YES' as IsNullable,
                k.COLUMN_NAME is not null as IsPrimaryKey,
                c.ORDINAL_POSITION as Ordinal,
                c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
                c.CHARACTER_OCTET_LENGTH as OctetLength,
                c.NUMERIC_PRECISION as NumericPrecision,
                c.NUMERIC_SCALE as NumericScale,
                c.DATETIME_PRECISION as DateTimePrecision,
                c.COLUMN_DEFAULT as DefaultValue,
                k.ORDINAL_POSITION as PrimaryKeyOrdinal
            from information_schema.COLUMNS c
            join information_schema.TABLES t
              on t.TABLE_SCHEMA = c.TABLE_SCHEMA and t.TABLE_NAME = c.TABLE_NAME
            left join information_schema.KEY_COLUMN_USAGE k
              on k.CONSTRAINT_NAME = 'PRIMARY' and k.TABLE_SCHEMA = c.TABLE_SCHEMA and k.TABLE_NAME = c.TABLE_NAME and k.COLUMN_NAME = c.COLUMN_NAME
            where c.TABLE_SCHEMA = database()
              and lower(c.TABLE_SCHEMA) = lower(coalesce(@schemaName, c.TABLE_SCHEMA))
            order by c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;
            """;

        return DatabaseSchemaBuilder.Build(databaseName,
                                           await SchemaColumnReader.ReadRowsAsync(command,
                                                                                  schemaName,
                                                                                  cancellationToken));
    }
}