using System.Text.Json;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Packaging;

public class ServerJsonTests
{
    private const string VersionPlaceholder = "0.0.0-placeholder";

    [Test]
    public async Task ServerJson_UsesCurrentMcpSchema()
    {
        using JsonDocument document = await LoadServerJsonAsync();
        JsonElement root = document.RootElement;

        root.GetProperty("$schema")
            .GetString()
            .ShouldBe("https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json");

        root.GetProperty("name")
            .GetString()
            .ShouldBe("io.github.KifoPL/RawSqlMcp");

        root.GetProperty("title")
            .GetString()
            .ShouldBe("Raw SQL MCP");

        root.GetProperty("description")
            .GetString()
            .ShouldNotBeNull()
            .ShouldContain("SQL Server");

        root.GetProperty("version")
            .GetString()
            .ShouldBe(VersionPlaceholder);

        root.GetProperty("repository")
            .GetProperty("url")
            .GetString()
            .ShouldBe("https://github.com/KifoPL/RawSqlMcp");
    }

    [Test]
    public async Task ServerJson_DeclaresNuGetPackageWithSchemaCorrectEnvironmentVariables()
    {
        using JsonDocument document = await LoadServerJsonAsync();
        JsonElement package = document.RootElement.GetProperty("packages")[0];

        package.GetProperty("registryType")
            .GetString()
            .ShouldBe("nuget");

        package.GetProperty("registryBaseUrl")
            .GetString()
            .ShouldBe("https://api.nuget.org/v3/index.json");

        package.GetProperty("identifier")
            .GetString()
            .ShouldBe("RawSqlMcp");

        package.GetProperty("version")
            .GetString()
            .ShouldBe(VersionPlaceholder);

        package.GetProperty("runtimeHint")
            .GetString()
            .ShouldBe("dnx");

        package.GetProperty("transport")
            .GetProperty("type")
            .GetString()
            .ShouldBe("stdio");

        package.TryGetProperty("environment_variables", out _)
            .ShouldBeFalse();

        JsonElement environmentVariable = package.GetProperty("environmentVariables")[0];

        environmentVariable.GetProperty("name")
            .GetString()
            .ShouldBe("RawSqlMcp__ConnectionStrings__Default");

        environmentVariable.GetProperty("isRequired")
            .GetBoolean()
            .ShouldBeFalse();

        environmentVariable.GetProperty("isSecret")
            .GetBoolean()
            .ShouldBeTrue();
    }

    private static async Task<JsonDocument> LoadServerJsonAsync()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                                                    "../../../../../src/RawSqlMcp.Cli/.mcp/server.json"));
        await using FileStream stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream);
    }
}