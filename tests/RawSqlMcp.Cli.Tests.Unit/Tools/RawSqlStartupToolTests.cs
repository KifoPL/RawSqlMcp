using RawSqlMcp.Cli.Services;
using RawSqlMcp.Cli.Tools;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Tools;

public class RawSqlStartupToolTests
{
    [Test]
    public void AvailableDatabases_ReturnsExpectedDatabases()
    {
        var resolver = new StubConnectionResolver(["Database1", "Database2"]);
        var tool = new RawSqlStartupTool(resolver);

        var result = tool.AvailableDatabases();

        result.ShouldNotBeNull();
        result.ShouldBe(["Database1", "Database2"]);
    }

    private sealed class StubConnectionResolver(string[] names) : IDatabaseConnectionResolver
    {
        public DatabaseConnectionDefinition Resolve(string databaseName)
            => throw new NotSupportedException();

        public string[] ListNames() => names;
    }
}