namespace RawSqlMcp.Cli.Models.Dtos;

public sealed record DatabaseSchemaDto(string DatabaseName, IReadOnlyList<SchemaDto> Schemas);