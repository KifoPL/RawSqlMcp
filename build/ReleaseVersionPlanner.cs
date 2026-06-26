using System.Text.RegularExpressions;

namespace RawSqlMcp.Build;

public sealed record ReleaseVersionPlan(bool ShouldCreateTag, string? Version, string? Tag, string Reason);

public static class ReleaseVersionPlanner
{
    private static readonly Regex StableTagPattern = new(
        @"^v(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SemanticVersionPattern = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReleaseMarkerPattern = new(
        @"\[release:\s*(?<value>[^\]\r\n]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static ReleaseVersionPlan Plan(IEnumerable<string> existingTags, string commitMessage)
    {
        HashSet<string> tagSet = existingTags
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        StableVersion latest = tagSet
            .Select(TryParseStableTag)
            .Where(x => x is not null)
            .Max() ?? new StableVersion(0, 0, 0);

        string requested = GetRequestedRelease(commitMessage);
        if (string.Equals(requested, "skip", StringComparison.OrdinalIgnoreCase))
            return new ReleaseVersionPlan(false, null, null, "Release tag skipped by [release: skip].");

        string version = requested.ToLowerInvariant() switch
        {
            "major" => latest.NextMajor().ToString(),
            "minor" => latest.NextMinor().ToString(),
            "patch" => latest.NextPatch().ToString(),
            _ when IsSemanticVersion(requested) => requested,
            _ => throw new InvalidOperationException($"Unsupported release override '{requested}'. Use major, minor, patch, skip, or an explicit semantic version.")
        };

        string tag = $"v{version}";
        if (tagSet.Contains(tag))
            throw new InvalidOperationException($"Release tag '{tag}' already exists.");

        return new ReleaseVersionPlan(true, version, tag, requested);
    }

    private static string GetRequestedRelease(string commitMessage)
    {
        MatchCollection matches = ReleaseMarkerPattern.Matches(commitMessage);
        return matches.Count == 0
            ? "patch"
            : matches[^1].Groups["value"].Value.Trim();
    }

    private static bool IsSemanticVersion(string value) => SemanticVersionPattern.IsMatch(value);

    private static StableVersion? TryParseStableTag(string tag)
    {
        Match match = StableTagPattern.Match(tag);
        if (!match.Success)
            return null;

        return new StableVersion(
            int.Parse(match.Groups["major"].Value),
            int.Parse(match.Groups["minor"].Value),
            int.Parse(match.Groups["patch"].Value));
    }

    private sealed record StableVersion(int Major, int Minor, int Patch) : IComparable<StableVersion>
    {
        public StableVersion NextMajor() => new(Major + 1, 0, 0);

        public StableVersion NextMinor() => new(Major, Minor + 1, 0);

        public StableVersion NextPatch() => new(Major, Minor, Patch + 1);

        public int CompareTo(StableVersion? other)
        {
            if (other is null)
                return 1;

            int major = Major.CompareTo(other.Major);
            if (major != 0)
                return major;

            int minor = Minor.CompareTo(other.Minor);
            return minor != 0 ? minor : Patch.CompareTo(other.Patch);
        }

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}