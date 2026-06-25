using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Tools;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Tools;

public class RawSqlStartupToolTests
{
    [Test]
    public void AvailableDatabases_ReturnsExpectedDatabases()
    {
        // Arrange
        var options = Options.Create(new RawSqlOptions
        {
            ConnectionStrings = new()
            {
                { "Database1", "ConnectionString1" },
                { "Database2", "ConnectionString2" }
            }
        });

        var tool = new RawSqlStartupTool(options);

        // Act
        var result = tool.AvailableDatabases();

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(2);
        result.ShouldContain("Database1");
        result.ShouldContain("Database2");
    }
}