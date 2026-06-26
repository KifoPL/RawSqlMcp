using System.Data.Common;
using System.Globalization;

namespace RawSqlMcp.Cli.Services.Schema;

public static class SchemaColumnReader
{
    public static async Task<IReadOnlyList<SchemaColumnRow>> ReadRowsAsync(DbCommand command,
                                                                           CancellationToken cancellationToken)
    {
        List<SchemaColumnRow> rows = [];
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                         ReadString(reader,
                                    "DatabaseName"),
                         ReadString(reader,
                                    "SchemaName"),
                         ReadString(reader,
                                    "TableName"),
                         ReadNullableString(reader,
                                            "TableType"),
                         ReadString(reader,
                                    "ColumnName"),
                         ReadString(reader,
                                    "DataType"),
                         ReadNullableBool(reader,
                                          "IsNullable"),
                         ReadNullableBool(reader,
                                          "IsPrimaryKey"),
                         ReadInt32(reader,
                                   "Ordinal"),
                         ReadNullableInt32(reader,
                                           "MaxLength"),
                         ReadNullableInt32(reader,
                                           "OctetLength"),
                         ReadNullableInt32(reader,
                                           "NumericPrecision"),
                         ReadNullableInt32(reader,
                                           "NumericScale"),
                         ReadNullableInt32(reader,
                                           "DateTimePrecision"),
                         ReadNullableString(reader,
                                            "DefaultValue"),
                         ReadNullableInt32(reader,
                                           "PrimaryKeyOrdinal")));
        }

        return rows;
    }

    private static string ReadString(DbDataReader reader,
                                     string name)
        => Convert.ToString(reader[name],
                            CultureInfo.InvariantCulture)
           ?? string.Empty;

    private static string? ReadNullableString(DbDataReader reader,
                                              string name)
        => reader[name] == DBNull.Value
               ? null
               : Convert.ToString(reader[name],
                                  CultureInfo.InvariantCulture);

    private static int ReadInt32(DbDataReader reader,
                                 string name)
        => Convert.ToInt32(reader[name],
                           CultureInfo.InvariantCulture);

    private static int? ReadNullableInt32(DbDataReader reader,
                                          string name)
        => reader[name] == DBNull.Value
               ? null
               : Convert.ToInt32(reader[name],
                                 CultureInfo.InvariantCulture);

    private static bool? ReadNullableBool(DbDataReader reader,
                                          string name)
        => reader[name] == DBNull.Value
               ? null
               : Convert.ToBoolean(reader[name],
                                   CultureInfo.InvariantCulture);
}