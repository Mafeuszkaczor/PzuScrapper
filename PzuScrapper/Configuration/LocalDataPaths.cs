namespace PzuScrapper.Configuration;

/// <summary>
/// Playwright full storage state file (browser-state) per environment (e.g. browser-state.Development.json) or default.
/// Environment name comes from <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.EnvironmentName"/> (e.g. Development, Production).
/// </summary>
internal static class LocalDataPaths
{
    public static string ResolveBrowserStorageStatePath(string environmentName) =>
        ResolveFirstExisting("browser-state", environmentName);

    private static string ResolveFirstExisting(string prefix, string environmentName)
    {
        foreach (var path in Candidates(prefix, environmentName))
        {
            if (File.Exists(path))
                return path;
        }

        return $"{prefix}.json";
    }

    private static IEnumerable<string> Candidates(string prefix, string environmentName)
    {
        yield return $"{prefix}.{environmentName}.json";
        yield return $"{prefix}.{environmentName.ToLowerInvariant()}.json";
        yield return $"{prefix}.json";
    }
}
