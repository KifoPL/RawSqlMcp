using RawSqlMcp.Build;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Packaging;

public class PreCommitChangeDetectorTests
{
    [Test]
    public void HasChangedTapeFiles_ReturnsFalseWhenOnlySourceFilesChanged()
    {
        const string status = """
 M build/Build.cs
A  README.md
?? docs/vhs/install-run.gif
""";

        PreCommitChangeDetector.HasChangedTapeFiles(status).ShouldBeFalse();
    }

    [Test]
    public void HasChangedTapeFiles_ReturnsTrueWhenTapeFileChanged()
    {
        const string status = """
 M docs/vhs/install-run.tape
M  src/RawSqlMcp.Cli/Program.cs
""";

        PreCommitChangeDetector.HasChangedTapeFiles(status).ShouldBeTrue();
    }
}