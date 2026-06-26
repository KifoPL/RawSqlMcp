using System.Data.Common;
using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Services.Schema;

public sealed class MySqlSchemaReader : IDatabaseSchemaReader
{
    public async Task<DatabaseSchemaDto> ReadSchemaAsync(
        DbConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            select
                c.TABLE_SCHEMA as DatabaseName,
                c.TABLE_SCHEMA as SchemaName,
                c.TABLE_NAME as TableName,
                t.TABLE_TYPE as TableType,
                c.COLUMN_NAME as ColumnName,
                c.DATA_TYPE as DataType,
                c.IS_NULLABLE = 'YES' as IsNullable,
                c.COLUMN_KEY = 'PRI' as IsPrimaryKey,
                c.ORDINAL_POSITION as Ordinal,
                c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
                c.CHARACTER_OCTET_LENGTH as OctetLength,
                c.NUMERIC_PRECISION as NumericPrecision,
                c.NUMERIC_SCALE as NumericScale,
                c.DATETIME_PRECISION as DateTimePrecision,
                c.COLUMN_DEFAULT as DefaultValue,
                case when c.COLUMN_KEY = 'PRI' then c.ORDINAL_POSITION else null end as PrimaryKeyOrdinal
            from information_schema.COLUMNS c
            join information_schema.TABLES t
              on t.TABLE_SCHEMA = c.TABLE_SCHEMA and t.TABLE_NAME = c.TABLE_NAME
            where c.TABLE_SCHEMA = database()
            order by c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;
            """;

        return DatabaseSchemaBuilder.Build(connection.Database,
                                           await SchemaColumnReader.ReadRowsAsync(command,
                                                                                  cancellationToken));
    }
}