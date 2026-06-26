using RawSqlMcp.Build;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Packaging;

public class GitHubActionsCredentialCleanerTests
{
    [Test]
    public void GetCheckoutCredentialKeysToUnset_ReturnsCheckoutIncludeIfPathKeys()
    {
        const string configNames = """
includeif.gitdir:/home/runner/work/RawSqlMcp/RawSqlMcp/.git.path
includeif.gitdir:/home/runner/work/RawSqlMcp/RawSqlMcp/.git/worktrees/*.path
remote.origin.url
""";

        GitHubActionsCredentialCleaner.GetCheckoutCredentialKeysToUnset(configNames)
            .ShouldBe([
                "includeif.gitdir:/home/runner/work/RawSqlMcp/RawSqlMcp/.git.path",
                "includeif.gitdir:/home/runner/work/RawSqlMcp/RawSqlMcp/.git/worktrees/*.path"
            ]);
    }
}