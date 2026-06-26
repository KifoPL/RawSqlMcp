namespace _build;

public sealed record ReleaseVersionPlan(
    bool ShouldCreateTag,
    string? Version,
    string? Tag,
    string Reason);