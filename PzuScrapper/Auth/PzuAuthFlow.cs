using Microsoft.Playwright;
using PlaywrightTests;
using PzuScrapper.Models;

namespace PzuScrapper.Auth;

/// <summary>Wznowienie sesji z plików lub logowanie UI + weryfikacja URL listy aukcji.</summary>
internal sealed class PzuAuthFlow
{
    private const string SearchUrlPattern = "**/bidder/auction/search";
    private const string SearchPathFragment = "bidder/auction/search";

    private readonly IPage _page;
    private readonly SiteSession _siteSession;

    public PzuAuthFlow(IPage page, SiteSession siteSession)
    {
        _page = page;
        _siteSession = siteSession;
    }

    public async Task<bool> EstablishSessionAsync()
    {
        Console.WriteLine("[Scrapper] Próba wznowienia sesji (cookies + sessionStorage)...");
        if (await TryOpenSearchPageAsync())
            Console.WriteLine("[Scrapper] Sesja z plików aktywna – pomijam formularz logowania.");
        else
        {
            Console.WriteLine("[Scrapper] Sesja z plików nie wystarczyła – logowanie formularzem.");
            await new Login(_page, _siteSession).LoginAsync();
        }

        Console.WriteLine("[Scrapper] Weryfikuję logowanie...");
        try
        {
            await _page.WaitForURLAsync(SearchUrlPattern, new PageWaitForURLOptions { Timeout = 30_000 });
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Console.WriteLine("[Scrapper] Zalogowano pomyślnie.");
            return true;
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[Scrapper] BŁĄD: Logowanie nie powiodło się. Aktualna strona: {_page.Url}");
            return false;
        }
    }

    private async Task<bool> TryOpenSearchPageAsync()
    {
        try
        {
            await _page.GotoAsync(
                "https://ppo.pzu.pl/bidder/auction/search",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60_000 });
            await _page.WaitForTimeoutAsync(1500);
        }
        catch (TimeoutException)
        {
            return false;
        }

        return _page.Url.Contains(SearchPathFragment, StringComparison.OrdinalIgnoreCase);
    }
}
