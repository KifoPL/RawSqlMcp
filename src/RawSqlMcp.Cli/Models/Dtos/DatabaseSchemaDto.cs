using System.Data;
using System.Globalization;

namespace RawSqlMcp.Cli.Models.Dtos;

public sealed record DatabaseSchemaDto(string DatabaseName, IReadOnlyList<SchemaDto> Schemas)
{
    public static implicit operator DatabaseSchemaDto(DataTable dataTable)
    {
        ArgumentNullException.ThrowIfNull(dataTable);

        string DatabaseName()
        {
            foreach (DataRow row in dataTable.Rows)
            {
                string value = ReadFirstString(row,
                                               "TABLE_CATALOG",
                                               "TABLE_CAT",
                                               "DATABASE_NAME",
                                               "DatabaseName");
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return dataTable.TableName;
        }

        string ReadTableSchema(DataRow row)
            => ReadFirstString(row,
                               "TABLE_SCHEMA",
                               "TABLE_SCHEM",
                               "OWNER",
                               "SCHEMA_NAME",
                               "SchemaName",
                               "Schema");

        string ReadTableName(DataRow row)
            => ReadFirstString(row,
                               "TABLE_NAME",
                               "TableName",
                               "TABLE",
                               "Table");

        string ReadColumnName(DataRow row)
            => ReadFirstString(row,
                               "COLUMN_NAME",
                               "ColumnName",
                               "COLUMN",
                               "Column",
                               "Name");

        string ReadFirstString(DataRow row,
                               params string[] columnNames)
        {
            foreach (string columnName in columnNames)
            {
                string? value = ReadStringOrNull(row,
                                                 columnName);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        string? ReadStringOrNull(DataRow row,
                                 string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return null;

            return Convert.ToString(row[columnName],
                                    CultureInfo.InvariantCulture);
        }

        int? ReadInt32OrNull(DataRow row,
                             string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return null;

            return Convert.ToInt32(row[columnName],
                                   CultureInfo.InvariantCulture);
        }

        bool? ReadYesNoOrNull(DataRow row,
                              string columnName)
            => ReadStringOrNull(row,
                                columnName) is { } value
                   ? string.Equals(value,
                                   "YES",
                                   StringComparison.OrdinalIgnoreCase)
                   : null;

        var tableDtos = dataTable.Rows.Cast<DataRow>()
                                 .Where(row => !string.IsNullOrWhiteSpace(ReadTableName(row)))
                                 .GroupBy(row => new
                                 {
                                     SchemaName = ReadTableSchema(row),
                                     Name = ReadTableName(row)
                                 })
                                 .Select(group => new TableDto(
                                             group.Key.SchemaName,
                                             group.Key.Name,
                                             ReadStringOrNull(group.First(),
                                                              "TABLE_TYPE"),
                                             group.OrderBy(row => ReadInt32OrNull(row,
                                                                      "ORDINAL_POSITION")
                                                               ?? 0)
                                                  .Select(row => new ColumnDto(
                                                              ReadColumnName(row),
                                                              ReadFirstString(row,
                                                                              "DATA_TYPE",
                                                                              "TYPE_NAME"),
                                                              ReadYesNoOrNull(row,
                                                                              "IS_NULLABLE"),
                                                              null,
                                                              ReadInt32OrNull(row,
                                                                              "ORDINAL_POSITION")
                                                           ?? 0,
                                                              ReadInt32OrNull(row,
                                                                              "CHARACTER_MAXIMUM_LENGTH"),
                                                              ReadInt32OrNull(row,
                                                                              "CHARACTER_OCTET_LENGTH"),
                                                              ReadInt32OrNull(row,
                                                                              "NUMERIC_PRECISION"),
                                                              ReadInt32OrNull(row,
                                                                              "NUMERIC_SCALE"),
                                                              ReadInt32OrNull(row,
                                                                              "DATETIME_PRECISION"),
                                                              ReadStringOrNull(row,
                                                                               "COLUMN_DEFAULT"),
                                                              null))
                                                  .ToArray()))
                                 .OrderBy(table => table.SchemaName,
                                          StringComparer.Ordinal)
                                 .ThenBy(table => table.Name,
                                         StringComparer.Ordinal)
                                 .ToArray();

        var schemas = tableDtos.GroupBy(table => table.SchemaName)
                               .Select(group => new SchemaDto(group.Key,
                                                              group.ToArray()))
                               .OrderBy(schema => schema.Name,
                                        StringComparer.Ordinal)
                               .ToArray();

        return new(DatabaseName(),
                   schemas);
    }
}