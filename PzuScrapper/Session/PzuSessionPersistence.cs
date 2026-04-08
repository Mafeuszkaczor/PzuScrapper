using Microsoft.Playwright;

namespace PzuScrapper.Session;

/// <summary>Łączy cookies i sessionStorage: przywracanie przed startem, zapis po zalogowaniu.</summary>
internal sealed class PzuSessionPersistence
{
    private readonly CookieFileStore _cookies = new();
    private readonly SessionStorageFileStore _storage = new();

    public async Task RestoreAsync(IBrowserContext context)
    {
        await _cookies.LoadIntoAsync(context);
        await _storage.InjectIntoAsync(context);
    }

    public async Task SaveAsync(IBrowserContext context, IPage page)
    {
        await _cookies.SaveFromAsync(context);
        await _storage.SaveFromAsync(page);
    }
}
