using System.Data.Common;
using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Services.Schema;

public sealed class SqliteSchemaReader : IDatabaseSchemaReader
{
    public async Task<DatabaseSchemaDto> ReadSchemaAsync(
        DbConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            select
                @databaseName as DatabaseName,
                'main' as SchemaName,
                m.name as TableName,
                m.type as TableType,
                p.name as ColumnName,
                p.type as DataType,
                case p."notnull" when 0 then 1 else 0 end as IsNullable,
                case when p.pk > 0 then 1 else 0 end as IsPrimaryKey,
                p.cid + 1 as Ordinal,
                null as MaxLength,
                null as OctetLength,
                null as NumericPrecision,
                null as NumericScale,
                null as DateTimePrecision,
                p.dflt_value as DefaultValue,
                nullif(p.pk, 0) as PrimaryKeyOrdinal
            from sqlite_master m
            join pragma_table_xinfo(m.name) p
            where m.type in ('table', 'view')
              and m.name not like 'sqlite_%'
              and p.hidden = 0
            order by m.name, p.cid;
            """;

        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "@databaseName";
        parameter.Value = databaseName;
        command.Parameters.Add(parameter);

        return DatabaseSchemaBuilder.Build(databaseName,
                                           await SchemaColumnReader.ReadRowsAsync(command,
                                                                                  cancellationToken));
    }
}