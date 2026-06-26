using RawSqlMcp.Build;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Packaging;

public class ReleaseVersionPlannerTests
{
    [Test]
    public void Plan_DefaultsToPatchBumpFromLatestStableTag()
    {
        ReleaseVersionPlan plan = ReleaseVersionPlanner.Plan(["v1.2.3", "v1.2.2"], "Merge pull request #12");

        plan.ShouldCreateTag.ShouldBeTrue();
        plan.Version.ShouldBe("1.2.4");
        plan.Tag.ShouldBe("v1.2.4");
    }

    [Test]
    public void Plan_SupportsMinorOverrideFromCommitMessage()
    {
        ReleaseVersionPlan plan = ReleaseVersionPlanner.Plan(["v1.2.3"], "Merge pull request #12\n\n[release: minor]");

        plan.ShouldCreateTag.ShouldBeTrue();
        plan.Version.ShouldBe("1.3.0");
        plan.Tag.ShouldBe("v1.3.0");
    }

    [Test]
    public void Plan_SupportsExplicitSemVerOverrideFromCommitMessage()
    {
        ReleaseVersionPlan plan = ReleaseVersionPlanner.Plan(["v1.2.3"], "Merge pull request #12\n\n[release: 2.0.0-beta.1]");

        plan.ShouldCreateTag.ShouldBeTrue();
        plan.Version.ShouldBe("2.0.0-beta.1");
        plan.Tag.ShouldBe("v2.0.0-beta.1");
    }

    [Test]
    public void Plan_SupportsSkippingReleaseTag()
    {
        ReleaseVersionPlan plan = ReleaseVersionPlanner.Plan(["v1.2.3"], "Merge pull request #12\n\n[release: skip]");

        plan.ShouldCreateTag.ShouldBeFalse();
        plan.Version.ShouldBeNull();
        plan.Tag.ShouldBeNull();
    }

    [Test]
    public void Plan_StartsAtPatchOneWhenNoStableTagsExist()
    {
        ReleaseVersionPlan plan = ReleaseVersionPlanner.Plan(["v1.0.0-alpha.1", "not-a-version"], "Initial merge");

        plan.ShouldCreateTag.ShouldBeTrue();
        plan.Version.ShouldBe("0.0.1");
        plan.Tag.ShouldBe("v0.0.1");
    }
}