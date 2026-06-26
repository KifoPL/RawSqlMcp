namespace _build;

public static class GitHubActionsCredentialCleaner
{
    public static IReadOnlyList<string> GetCheckoutCredentialKeysToUnset(string configNames)
    {
        return configNames
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsCheckoutCredentialInclude)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsCheckoutCredentialInclude(string key) =>
        key.StartsWith("includeif.gitdir:", StringComparison.OrdinalIgnoreCase) &&
        key.EndsWith(".path", StringComparison.OrdinalIgnoreCase);
}