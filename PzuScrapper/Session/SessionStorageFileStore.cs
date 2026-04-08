using Microsoft.Playwright;
using PzuScrapper.Configuration;

namespace PzuScrapper.Session;

internal sealed class SessionStorageFileStore
{
    private readonly string _path;

    public SessionStorageFileStore(string? path = null)
    {
        _path = path ?? LocalDataPaths.SessionStorage;
    }

    public async Task InjectIntoAsync(IBrowserContext context)
    {
        if (!File.Exists(_path))
            return;

        var sessionJson = await File.ReadAllTextAsync(_path);
        await context.AddInitScriptAsync($@"
            (storageJson => {{
                if (!storageJson) return;
                const storage = JSON.parse(storageJson);
                if (window.location.hostname === 'ppo.pzu.pl' ||
                    window.location.hostname.endsWith('.ppo.pzu.pl')) {{
                        for (const [key, value] of Object.entries(storage)) {{
                            if (value) window.sessionStorage.setItem(key, value);
                        }}
                }}
            }})('{sessionJson.Replace("'", "\\'")}')
        ");
    }

    public async Task SaveFromAsync(IPage page)
    {
        var sessionData = await page.EvaluateAsync<string>(
            "() => JSON.stringify(Object.fromEntries(Object.entries(sessionStorage)))");
        await File.WriteAllTextAsync(_path, sessionData);
        Console.WriteLine("SessionStorage zapisany.");
    }
}
