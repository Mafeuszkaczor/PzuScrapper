using System.Runtime.InteropServices;

namespace PzuScrapper.Configuration;

internal static class AppPaths
{
    // ─── App data (per-user writable directory) ───────────────────────────

    private static string AppDataDirectory => EnsureDirectory(GetWritableAppDataDirectory());

    /// <summary>JSONL file tracking processed auction IDs.</summary>
    public static string CarsJsonLinesPath => Path.Combine(AppDataDirectory, "cars.jsonl");

    /// <summary>Working photo folder: AppData/PzuScrapper/photos/{VIN or auction number}/</summary>
    public static string PhotosDirectory => Path.Combine(AppDataDirectory, "photos");

    /// <summary>Per-user credentials file (login + password).</summary>
    public static string CredentialsPath => Path.Combine(AppDataDirectory, "credentials.json");

    // ─── Desktop export (Desktop/import/dd.MM.yyyy/) ──────────────────────

    /// <summary>Today's PDF output folder on the Desktop.</summary>
    public static string TodayImportDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "import",
            DateTime.Today.ToString("dd.MM.yyyy"));

    public static string PdfPathForAuction(string auctionUniqueNumber) =>
        Path.Combine(TodayImportDirectory, $"{SanitizeFileName(auctionUniqueNumber)}.pdf");

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var s = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(s) ? "auction" : s.Trim();
    }

    // ─── Playwright browser state ──────────────────────────────────────────

    /// <summary>Resolves browser-state JSON path by environment name (Development, Production, …).</summary>
    public static string ResolveBrowserStorageStatePath(string environmentName) =>
        ResolveFirstExisting("browser-state", environmentName);

    private static string ResolveFirstExisting(string prefix, string environmentName)
    {
        foreach (var path in BrowserStatePathCandidates(prefix, environmentName))
            if (File.Exists(path))
                return path;
        return $"{prefix}.json";
    }

    private static IEnumerable<string> BrowserStatePathCandidates(string prefix, string environmentName)
    {
        yield return $"{prefix}.{environmentName}.json";
        yield return $"{prefix}.{environmentName.ToLowerInvariant()}.json";
        yield return $"{prefix}.json";
    }

    private static string GetWritableAppDataDirectory()
    {
        // macOS: ~/Library/Application Support/PzuScrapper
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "PzuScrapper");
        }

        // Windows/Linux: prefer LocalApplicationData (per-user, writable).
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            return Path.Combine(localAppData, "PzuScrapper");

        // Last resort fallback if LocalApplicationData is unavailable.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
            return Path.Combine(appData, "PzuScrapper");

        var homeFallback = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeFallback, ".pzuscrapper");
    }

    private static string EnsureDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        return directory;
    }
}
