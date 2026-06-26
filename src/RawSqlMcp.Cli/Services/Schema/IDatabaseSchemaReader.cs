using System.Data.Common;
using RawSqlMcp.Cli.Models.Dtos;

namespace RawSqlMcp.Cli.Services.Schema;

public interface IDatabaseSchemaReader
{
    Task<DatabaseSchemaDto> ReadSchemaAsync(
        DbConnection connection,
        string databaseName,
        CancellationToken cancellationToken);
}