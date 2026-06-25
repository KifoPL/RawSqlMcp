namespace RawSqlMcp.Cli.Models.Dtos;

public sealed record ColumnDto(
    string Name,
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