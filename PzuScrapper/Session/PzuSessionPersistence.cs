using Microsoft.Playwright;
using PzuScrapper.Configuration;

namespace PzuScrapper.Session;

/// <summary>
/// Full Playwright session (cookies + localStorage + sessionStorage per origin) — read/write storage state file.
/// </summary>
internal sealed class PzuSessionPersistence
{
    private readonly string _path;

    public PzuSessionPersistence(string environmentName)
    {
        _path = LocalDataPaths.ResolveBrowserStorageStatePath(environmentName);
    }

    public BrowserNewContextOptions BuildNewContextOptions()
    {
        var options = new BrowserNewContextOptions();
        if (!File.Exists(_path))
            return options;

        // StorageState(string) is JSON or a path — a relative path like "browser-state.json" is mistaken for JSON (starts with "b").
        options.StorageStatePath = Path.GetFullPath(_path);
        Console.WriteLine("[Session] Wczytano zapisane logowanie.");
        return options;
    }

    public async Task SaveAsync(IBrowserContext context)
    {
        await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = _path });
        Console.WriteLine("[Session] Zapisano logowanie na przyszłe uruchomienia.");
    }
}
