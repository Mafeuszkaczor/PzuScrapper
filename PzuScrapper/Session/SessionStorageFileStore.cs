using Microsoft.Playwright;

namespace PzuScrapper.Session;

/// <summary>Odczyt i zapis session-storage.json oraz init script dla kontekstu.</summary>
internal sealed class SessionStorageFileStore
{
    private readonly string _path;

    public SessionStorageFileStore(string path = "session-storage.json")
    {
        _path = path;
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
                        window.sessionStorage.setItem(key, value);
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
