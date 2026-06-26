using _build;
using Shouldly;

namespace RawSqlMcp.Cli.Tests.Unit.Packaging;

public class CommitMessageLengthGateTests
{
    [Test]
    public void GetValidationError_ReturnsNullForSubjectAtLimit()
    {
        string message = new('a', CommitMessageLengthGate.MaxSubjectLength);

        CommitMessageLengthGate.GetValidationError(message)
                               .ShouldBeNull();
    }

    [Test]
    public void GetValidationError_ReturnsErrorForSubjectOverLimit()
    {
        string message = new('a', CommitMessageLengthGate.MaxSubjectLength + 1);

        string? error = CommitMessageLengthGate.GetValidationError(message);

        error.ShouldNotBeNull();
        error.ShouldContain("100 characters or fewer");
        error.ShouldContain("101");
    }

    [Test]
    public void GetValidationError_OnlyChecksSubjectLine()
    {
        string message = $"Short subject{Environment.NewLine}{Environment.NewLine}{new string('a', 150)}";

        CommitMessageLengthGate.GetValidationError(message)
                               .ShouldBeNull();
    }
}