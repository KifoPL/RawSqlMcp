namespace RawSqlMcp.Cli.Models.Dtos;

public sealed record SchemaDto(string Name, IReadOnlyList<TableDto> Tables);