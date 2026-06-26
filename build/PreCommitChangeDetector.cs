namespace RawSqlMcp.Build;

public static class PreCommitChangeDetector
{
    public static bool HasChangedTapeFiles(string porcelainStatus)
    {
        foreach (string line in porcelainStatus.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length <= 3)
                continue;

            string path = line[3..].Trim();
            if (path.Split(" -> ", StringSplitOptions.TrimEntries).Any(IsTapeFile))
                return true;
        }

        return false;
    }

    private static bool IsTapeFile(string path) =>
        path.EndsWith(".tape", StringComparison.OrdinalIgnoreCase);
}