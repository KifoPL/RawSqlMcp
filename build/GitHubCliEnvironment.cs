using System.Collections;

namespace RawSqlMcp.Build;

public static class GitHubCliEnvironment
{
    public static IReadOnlyDictionary<string, string> CreateForCurrentProcess()
    {
        Dictionary<string, string?> variables = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(
                entry => entry.Key.ToString()!,
                entry => entry.Value?.ToString(),
                StringComparer.Ordinal);

        return Create(variables);
    }

    public static IReadOnlyDictionary<string, string> Create(IReadOnlyDictionary<string, string?> variables)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);

        CopyIfPresent(variables, environment, "PATH");
        CopyIfPresent(variables, environment, "HOME");
        CopyIfPresent(variables, environment, "XDG_CONFIG_HOME");

        string? token = GetValue(variables, "GH_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            token = GetValue(variables, "GITHUB_TOKEN");

        if (!string.IsNullOrWhiteSpace(token))
            environment["GH_TOKEN"] = token!;

        return environment;
    }

    private static void CopyIfPresent(IReadOnlyDictionary<string, string?> source, IDictionary<string, string> target, string name)
    {
        string? value = GetValue(source, name);
        if (!string.IsNullOrWhiteSpace(value))
            target[name] = value!;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> source, string name) =>
        source.TryGetValue(name, out string? value) ? value : null;
}