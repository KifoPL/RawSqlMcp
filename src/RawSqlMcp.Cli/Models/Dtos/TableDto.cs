namespace RawSqlMcp.Cli.Models.Dtos;

public sealed record TableDto(
    string SchemaName,
    string Name,
    string? TableType,
    IReadOnlyList<ColumnDto> Columns);