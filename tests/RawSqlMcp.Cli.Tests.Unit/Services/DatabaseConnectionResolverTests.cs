using System.Reflection;
using Microsoft.Extensions.Options;
using RawSqlMcp.Cli.Models.Options;
using RawSqlMcp.Cli.Services;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Services;

public class DatabaseConnectionResolverTests
{
    [Test]
    public void Resolve_ReturnsProviderAwareDatabase()
    {
        var options = Options.Create(new RawSqlOptions
        {
            Databases = new()
            {
                ["Local"] = new RawSqlDatabaseOptions
                {
                    Provider = "sqlite",
                    ConnectionString = "Data Source=:memory:"
                }
            }
        });

        var resolver = new DatabaseConnectionResolver(options);

        DatabaseConnectionDefinition result = resolver.Resolve("Local");

        result.Name.ShouldBe("Local");
        result.Provider.ShouldBe("sqlite");
        result.ConnectionString.ShouldBe("Data Source=:memory:");
    }

    [Test]
    public void Resolve_TreatsLegacyConnectionStringsAsSqlServer()
    {
#pragma warning disable CS0618
        var options = Options.Create(new RawSqlOptions
        {
            ConnectionStrings = new()
            {
                ["Default"] = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
            }
        });
#pragma warning restore CS0618

        var resolver = new DatabaseConnectionResolver(options);

        DatabaseConnectionDefinition result = resolver.Resolve("Default");

        result.Provider.ShouldBe("sqlserver");
        result.ConnectionString.ShouldContain("Server=localhost");
    }

    [Test]
    public void ListNames_ReturnsProviderAwareAndLegacyNamesWithoutDuplicates()
    {
#pragma warning disable CS0618
        var options = Options.Create(new RawSqlOptions
        {
            ConnectionStrings = new() { ["Default"] = "Server=legacy" },
            Databases = new()
            {
                ["Default"] = new RawSqlDatabaseOptions
                {
                    Provider = "sqlite",
                    ConnectionString = "Data Source=:memory:"
                },
                ["Reporting"] = new RawSqlDatabaseOptions
                {
                    Provider = "postgres",
                    ConnectionString = "Host=localhost;Database=reporting"
                }
            }
        });
#pragma warning restore CS0618

        var resolver = new DatabaseConnectionResolver(options);

        resolver.ListNames().ShouldBe(["Default", "Reporting"]);
    }

    [Test]
    public void ConnectionStringsOption_IsMarkedObsolete()
    {
#pragma warning disable CS0618
        PropertyInfo property = typeof(RawSqlOptions).GetProperty(nameof(RawSqlOptions.ConnectionStrings))
                                ?? throw new InvalidOperationException("ConnectionStrings option missing.");
#pragma warning restore CS0618

        ObsoleteAttribute attribute = property.GetCustomAttribute<ObsoleteAttribute>()
                                      ?? throw new InvalidOperationException("ConnectionStrings option is not obsolete.");

        attribute.Message.ShouldNotBeNull();
        attribute.Message.ShouldContain("RawSqlMcp__Databases__");
    }
}