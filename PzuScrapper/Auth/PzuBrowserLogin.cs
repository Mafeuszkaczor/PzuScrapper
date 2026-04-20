using System.Globalization;
using Microsoft.Playwright;
using PzuScrapper.Models;

namespace PzuScrapper.Auth;

/// <summary>Home → My account → Log in, then optional credential form + 2FA.</summary>
internal sealed class PzuBrowserLogin
{
    private readonly IPage _page;
    private readonly SiteSession _siteSession;

    public PzuBrowserLogin(IPage page, SiteSession siteSession)
    {
        _page = page;
        _siteSession = siteSession;
    }

    public async Task NavigateHomeAndAcceptCookiesAsync()
    {
        await _page.GotoAsync("https://ppo.pzu.pl/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 10_000 });
        await _page.Locator("body").ClickAsync();

        var acceptCookies = _page.GetByRole(AriaRole.Button, new() { Name = "Akceptuj wszystkie" });
        if (await acceptCookies.IsVisibleAsync())
            await acceptCookies.ClickAsync();
    }

    public async Task OpenMojeKontoMenuAsync()
    {
        await _page.Locator("a").Filter(new() { HasText = "Moje konto" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsZalogujButtonVisibleAsync()
    {
        var zaloguj = _page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj" });
        return await zaloguj.IsVisibleAsync();
    }

    public async Task ClickZalogujAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj" }).ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(500);
    }

    public async Task<bool> IsLoginFormVisibleAsync()
    {
        var userBox = _page.GetByRole(AriaRole.Textbox, new() { Name = "Użytkownik" });
        return await userBox.IsVisibleAsync();
    }

    public async Task FillCredentialsAndSubmitAsync()
    {
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Użytkownik" }).ClickAsync();
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Użytkownik" }).FillAsync(_siteSession.Login);
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Hasło" }).ClickAsync();
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Hasło" }).FillAsync(_siteSession.Password);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj" }).ClickAsync();
    }

    public async Task TryTwoFactorIfPresentAsync()
    {
        try
        {
            await _page.WaitForSelectorAsync("#secfense_iframe", new PageWaitForSelectorOptions { Timeout = 5_000 });
            Console.WriteLine("[Login] Wymagana weryfikacja 2FA.");
            await TwoFactorAuthenticationAsync(_page);
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[Login] Brak 2FA.");
        }
    }

    private static async Task<IPage> TwoFactorAuthenticationAsync(IPage page)
    {
        var frame = page.Locator("#secfense_iframe").ContentFrame;
        var codeInput = frame.GetByRole(AriaRole.Textbox, new() { Name = "Wprowadź 6-cyfrowy kod z SMS" });

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string smsCode;
            while (true)
            {
                Console.WriteLine($"[2FA] Podaj 6-cyfrowy kod z SMS (próba {attempt}/{maxAttempts}):");
                Console.Out.Flush();
                var raw = Console.ReadLine() ?? string.Empty;
                smsCode = NormalizeSmsCodeInput(raw);
                if (IsValidSixDigitIntCode(smsCode))
                    break;
                Console.WriteLine("[2FA] Kod musi być 6-cyfrową liczbą (możesz użyć spacji). Spróbuj ponownie.");
            }

            await codeInput.ClickAsync();
            await codeInput.FillAsync(smsCode);
            await frame.GetByRole(AriaRole.Checkbox, new() { Name = "Zaufaj tej przeglądarce" }).CheckAsync();
            await frame.GetByRole(AriaRole.Button, new() { Name = "ZATWIERDŹ" }).ClickAsync();

            await page.WaitForTimeoutAsync(2000);
            if (!await codeInput.IsVisibleAsync())
                break;

            if (attempt == maxAttempts)
                Console.WriteLine("Przekroczono limit prób weryfikacji 2FA.");
            else
                Console.WriteLine("Nieprawidłowy kod – spróbuj jeszcze raz.");
        }

        return page;
    }

    private static string NormalizeSmsCodeInput(string input)
    {
        var trimmed = input.Trim();
        return string.Concat(trimmed.Where(c => !char.IsWhiteSpace(c)));
    }

    private static bool IsValidSixDigitIntCode(string normalized)
    {
        if (normalized.Length != 6 || !normalized.All(char.IsDigit))
            return false;
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
               && v is >= 0 and <= 999_999;
    }
}
