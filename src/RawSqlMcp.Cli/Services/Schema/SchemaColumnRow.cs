namespace RawSqlMcp.Cli.Services.Schema;

public sealed record SchemaColumnRow(
    string DatabaseName,
    string SchemaName,
    string TableName,
    string? TableType,
    string ColumnName,
    string DataType,
    bool? IsNullable,
    bool? IsPrimaryKey,
    int Ordinal,
    int? MaxLength,
    int? OctetLength,
    int? NumericPrecision,
    int? NumericScale,
    int? DateTimePrecision,
    string? DefaultValue,
    int? PrimaryKeyOrdinal);