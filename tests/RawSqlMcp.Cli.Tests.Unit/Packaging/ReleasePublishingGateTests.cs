using RawSqlMcp.Build;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Packaging;

public class ReleasePublishingGateTests
{
    [Test]
    public void ShouldPublish_ReturnsTrueForTagPush()
    {
        ReleasePublishingGate.ShouldPublish("push", "refs/tags/v1.2.3")
            .ShouldBeTrue();
    }

    [Test]
    public void ShouldPublish_ReturnsFalseForManualRunOnTag()
    {
        ReleasePublishingGate.ShouldPublish("workflow_dispatch", "refs/tags/v1.2.3")
            .ShouldBeFalse();
    }

    [Test]
    public void ShouldPublish_ReturnsFalseForManualRunOnBranch()
    {
        ReleasePublishingGate.ShouldPublish("workflow_dispatch", "refs/heads/master")
            .ShouldBeFalse();
    }
}