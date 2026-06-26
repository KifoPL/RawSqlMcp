using RawSqlMcp.Build;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Packaging;

public class GitHubCliEnvironmentTests
{
    [Test]
    public void Create_PreservesPathForGitHubCliChildProcesses()
    {
        IReadOnlyDictionary<string, string> environment = GitHubCliEnvironment.Create(
            new Dictionary<string, string?>
            {
                ["PATH"] = "/usr/bin:/bin",
                ["GITHUB_TOKEN"] = "github-token"
            });

        environment["PATH"].ShouldBe("/usr/bin:/bin");
        environment["GH_TOKEN"].ShouldBe("github-token");
    }

    [Test]
    public void Create_PrefersExplicitGhToken()
    {
        IReadOnlyDictionary<string, string> environment = GitHubCliEnvironment.Create(
            new Dictionary<string, string?>
            {
                ["PATH"] = "/usr/bin:/bin",
                ["GH_TOKEN"] = "gh-token",
                ["GITHUB_TOKEN"] = "github-token"
            });

        environment["GH_TOKEN"].ShouldBe("gh-token");
    }

    [Test]
    public void Create_DoesNotIncludeEmptyVariables()
    {
        IReadOnlyDictionary<string, string> environment = GitHubCliEnvironment.Create(
            new Dictionary<string, string?>
            {
                ["PATH"] = "",
                ["HOME"] = null,
                ["GITHUB_TOKEN"] = "github-token"
            });

        environment.ContainsKey("PATH").ShouldBeFalse();
        environment.ContainsKey("HOME").ShouldBeFalse();
        environment["GH_TOKEN"].ShouldBe("github-token");
    }
}