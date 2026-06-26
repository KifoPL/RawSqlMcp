namespace _build;

public static class CommitMessageLengthGate
{
    public const int MaxSubjectLength = 100;

    public static string? GetValidationError(string commitMessage)
    {
        string subject = GetSubject(commitMessage);
        int length = subject.Length;

        return length <= MaxSubjectLength
            ? null
            : $"Commit message subject must be {MaxSubjectLength} characters or fewer. Actual length: {length}.";
    }

    static string GetSubject(string commitMessage) =>
        commitMessage.Split(["\r\n", "\n"], StringSplitOptions.None)
                     .FirstOrDefault() ?? string.Empty;
}