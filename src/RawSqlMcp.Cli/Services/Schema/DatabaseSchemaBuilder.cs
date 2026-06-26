using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Services.Schema;

public static class DatabaseSchemaBuilder
{
    public static DatabaseSchemaDto Build(string databaseName,
                                          IEnumerable<SchemaColumnRow> rows)
    {
        TableDto[] tables = rows.Where(row => !string.IsNullOrWhiteSpace(row.TableName)
                                              && !string.IsNullOrWhiteSpace(row.ColumnName))
                                .GroupBy(row => new
                                {
                                    row.SchemaName,
                                    row.TableName,
                                    row.TableType
                                })
                                .Select(group => new TableDto(
                                            group.Key.SchemaName,
                                            group.Key.TableName,
                                            group.Key.TableType,
                                            group.OrderBy(row => row.Ordinal)
                                                 .Select(row => new ColumnDto(
                                                             row.ColumnName,
                                                             row.DataType,
                                                             row.IsNullable,
                                                             row.IsPrimaryKey,
                                                             row.Ordinal,
                                                             row.MaxLength,
                                                             row.OctetLength,
                                                             row.NumericPrecision,
                                                             row.NumericScale,
                                                             row.DateTimePrecision,
                                                             row.DefaultValue,
                                                             row.PrimaryKeyOrdinal))
                                                 .ToArray()))
                                .OrderBy(table => table.SchemaName,
                                         StringComparer.Ordinal)
                                .ThenBy(table => table.Name,
                                        StringComparer.Ordinal)
                                .ToArray();

        SchemaDto[] schemas = tables.GroupBy(table => table.SchemaName)
                                    .Select(group => new SchemaDto(group.Key,
                                                                   group.ToArray()))
                                    .OrderBy(schema => schema.Name,
                                             StringComparer.Ordinal)
                                    .ToArray();

        return new(databaseName,
                   schemas);
    }
}