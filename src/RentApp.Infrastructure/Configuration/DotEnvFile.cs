namespace RentApp.Infrastructure.Configuration;

/// <summary>
/// Minimal loader for the repo-root <c>.env</c> file used in local development, so the API,
/// EF Core tooling and Docker Compose all read one file. Never used outside Development:
/// real environments inject variables directly. Existing environment variables always win.
/// </summary>
public static class DotEnvFile
{
    public const string FileName = ".env";

    /// <summary>Walks up from <paramref name="startDirectory"/> and loads the first .env found.</summary>
    /// <returns>The path that was loaded, or null if none was found.</returns>
    public static string? LoadFromNearest(string startDirectory, int maxLevels = 6)
    {
        var directory = new DirectoryInfo(startDirectory);
        for (var level = 0; directory is not null && level <= maxLevels; level++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (!File.Exists(candidate))
            {
                continue;
            }

            foreach (var (key, value) in Parse(File.ReadLines(candidate)))
            {
                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return candidate;
        }

        return null;
    }

    /// <summary>Parses KEY=VALUE lines. Blank lines and # comments are skipped; matching surrounding quotes are removed.</summary>
    public static IEnumerable<KeyValuePair<string, string>> Parse(IEnumerable<string> lines)
    {
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            yield return new KeyValuePair<string, string>(key, value);
        }
    }
}
