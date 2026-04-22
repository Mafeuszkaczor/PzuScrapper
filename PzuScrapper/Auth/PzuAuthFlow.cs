using Microsoft.Playwright;
using PzuScrapper.Models;

namespace PzuScrapper.Auth;

/// <summary>
/// From home → My account → Log in → detect auction list (session) vs login form.
/// </summary>
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
        var login = new PzuBrowserLogin(_page, _siteSession);

        Log.Info("Auth", "Łączę z usługą…");
        await login.NavigateHomeAndAcceptCookiesAsync();
        await login.OpenMojeKontoMenuAsync();

        if (!await login.IsZalogujButtonVisibleAsync())
        {
            Log.Info("Auth", "Sprawdzam, czy jesteś już zalogowany…");
            return await TryEnsureAuctionSearchViewAsync();
        }

        Log.Info("Auth", "Logowanie…");
        await login.ClickZalogujAsync();

        if (await WaitUntilAuctionSearchOrLoginFormAsync(login, TimeSpan.FromSeconds(10)))
        {
            if (IsAuctionSearchUrl(_page.Url))
            {
                Log.Info("Auth", "Sesja aktywna");
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                return true;
            }

            if (await login.IsLoginFormVisibleAsync())
            {
                await login.FillCredentialsAndSubmitAsync();
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await login.TryTwoFactorIfPresentAsync();
            }
        }
        else
        {
            Log.Info("Auth", "Czekam na formularz lub zakończenie logowania (to może chwilę potrwać)…");
        }

        Log.Info("Auth", "Sprawdzam dostęp do listy ofert…");
        try
        {
            await _page.WaitForURLAsync(SearchUrlPattern, new PageWaitForURLOptions { Timeout = 20_000 });
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Log.Info("Auth", "Zalogowano!");
            return true;
        }
        catch (TimeoutException)
        {
            Log.Error("Auth", "Nie udało się otworzyć listy ofert. Sprawdź logowanie lub spróbuj ponownie.");
            return false;
        }
    }

    private static bool IsAuctionSearchUrl(string url) =>
        url.Contains(SearchPathFragment, StringComparison.OrdinalIgnoreCase);

    private async Task<bool> TryEnsureAuctionSearchViewAsync()
    {
        try
        {
            await _page.GotoAsync(
                "https://ppo.pzu.pl/bidder/auction/search",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 15_000 });
            await _page.WaitForTimeoutAsync(1000);
            if (IsAuctionSearchUrl(_page.Url))
            {
                Log.Info("Auth", "Sesja aktywna, kontynuuję.");
                return true;
            }
        }
        catch (TimeoutException)
        {
        }

        Log.Error("Auth", "Nie udało się otworzyć listy ofert. Możesz zalogować się ręcznie w oknie przeglądarki i uruchomić program ponownie.");
        return false;
    }

    private async Task<bool> WaitUntilAuctionSearchOrLoginFormAsync(PzuBrowserLogin login, TimeSpan maxWait)
    {
        var deadline = DateTime.UtcNow + maxWait;
        while (DateTime.UtcNow < deadline)
        {
            if (IsAuctionSearchUrl(_page.Url))
                return true;
            if (await login.IsLoginFormVisibleAsync())
                return true;
            await Task.Delay(250);
        }

        return false;
    }
}
