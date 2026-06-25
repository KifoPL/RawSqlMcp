using System.Text.Json.Serialization;
using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Models;

[JsonSerializable(typeof(DatabaseSchemaDto))]
[JsonSerializable(typeof(SchemaDto))]
[JsonSerializable(typeof(TableDto))]
[JsonSerializable(typeof(ColumnDto))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(string[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class RawSqlJsonContext : JsonSerializerContext;