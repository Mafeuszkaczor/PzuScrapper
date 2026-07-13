using System.Text.Json;
using PzuScrapper.Models;

namespace PzuScrapper.Configuration;

/// <summary>
/// Persists PZU login + password in the per-user AppData folder
/// (<c>~/Library/Application Support/PzuScrapper/credentials.json</c> on macOS).
/// This is the single source of truth for credentials — set them from the
/// in-app settings menu or on first launch.
/// </summary>
internal static class CredentialsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static bool Exists => File.Exists(AppPaths.CredentialsPath);

    public static SiteSession? TryLoad()
    {
        try
        {
            var path = AppPaths.CredentialsPath;
            if (!File.Exists(path))
                return null;

            var entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(path));
            if (entry is null
                || string.IsNullOrWhiteSpace(entry.Login)
                || string.IsNullOrWhiteSpace(entry.Password))
            {
                return null;
            }

            return new SiteSession { Login = entry.Login!, Password = entry.Password! };
        }
        catch (Exception ex)
        {
            Log.Warn("Ustawienia", $"Nie udało się odczytać zapisanych danych logowania: {ex.Message}");
            return null;
        }
    }

    public static void Save(string login, string password)
    {
        var path = AppPaths.CredentialsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(
            new Entry { Login = login, Password = password },
            JsonOpts);
        File.WriteAllText(path, json);
        TrySetOwnerOnlyPermissions(path);
    }

    private static void TrySetOwnerOnlyPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // best effort — file is still usable without 0600 perms
        }
    }

    private sealed class Entry
    {
        public string? Login { get; set; }
        public string? Password { get; set; }
    }
}
