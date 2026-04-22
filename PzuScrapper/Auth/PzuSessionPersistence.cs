using Microsoft.Playwright;
using PzuScrapper.Configuration;

namespace PzuScrapper.Auth;

/// <summary>
/// Full Playwright session (cookies + localStorage + sessionStorage per origin) — read/write storage state file.
/// </summary>
internal sealed class PzuSessionPersistence
{
    private readonly string _path;

    public PzuSessionPersistence(string environmentName)
    {
        _path = AppPaths.ResolveBrowserStorageStatePath(environmentName);
    }

    public BrowserNewContextOptions BuildNewContextOptions()
    {
        var options = new BrowserNewContextOptions();
        if (!File.Exists(_path))
            return options;

        options.StorageStatePath = Path.GetFullPath(_path);
        Log.Info("Session", "Wczytano zapisane logowanie.");
        return options;
    }

    public async Task SaveAsync(IBrowserContext context)
    {
        await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = _path });
        Log.Info("Session", "Zapisano logowanie na przyszłe uruchomienia.");
    }
}
