namespace RawSqlMcp.Build;

public static class ReleasePublishingGate
{
    public static bool ShouldPublish(string? eventName, string? gitHubRef) =>
        string.Equals(eventName, "push", StringComparison.OrdinalIgnoreCase) &&
        gitHubRef?.StartsWith("refs/tags/v", StringComparison.Ordinal) == true;
}