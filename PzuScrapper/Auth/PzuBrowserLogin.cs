using System.Globalization;
using Microsoft.Playwright;
using PzuScrapper.Models;

namespace PzuScrapper.Auth;

/// <summary>Home → My account → Log in, then optional credential form + 2FA.</summary>
internal sealed class PzuBrowserLogin
{
    private const string HomeUrl = "https://ppo.pzu.pl/";
    private const string TwoFactorIframeSelector = "#secfense_iframe";

    private readonly IPage _page;
    private readonly SiteSession _siteSession;

    public PzuBrowserLogin(IPage page, SiteSession siteSession)
    {
        _page = page;
        _siteSession = siteSession;
    }

    // ─── Page locators (one place, descriptive names) ──────────────────────

    private ILocator AcceptCookiesButton => _page.GetByRole(AriaRole.Button, new() { Name = "Akceptuj wszystkie" });
    private ILocator MojeKontoLink => _page.Locator("a").Filter(new() { HasText = "Moje konto" });
    private ILocator ZalogujButton => _page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj" });
    private ILocator UserTextbox => _page.GetByRole(AriaRole.Textbox, new() { Name = "Użytkownik" });
    private ILocator PasswordTextbox => _page.GetByRole(AriaRole.Textbox, new() { Name = "Hasło" });

    // ─── Steps ─────────────────────────────────────────────────────────────

    public async Task NavigateHomeAndAcceptCookiesAsync()
    {
        await _page.GotoAsync(HomeUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 10_000 });
        await _page.Locator("body").ClickAsync();

        if (await AcceptCookiesButton.IsVisibleAsync())
            await AcceptCookiesButton.ClickAsync();
    }

    public async Task OpenMojeKontoMenuAsync()
    {
        await MojeKontoLink.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public Task<bool> IsZalogujButtonVisibleAsync() => ZalogujButton.IsVisibleAsync();

    public async Task ClickZalogujAsync()
    {
        await ZalogujButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.WaitForTimeoutAsync(500);
    }

    public Task<bool> IsLoginFormVisibleAsync() => UserTextbox.IsVisibleAsync();

    public async Task FillCredentialsAndSubmitAsync()
    {
        await UserTextbox.ClickAsync();
        await UserTextbox.FillAsync(_siteSession.Login);
        await PasswordTextbox.ClickAsync();
        await PasswordTextbox.FillAsync(_siteSession.Password);
        await ZalogujButton.ClickAsync();
    }

    public async Task TryTwoFactorIfPresentAsync()
    {
        if (!await WaitForTwoFactorIframeAsync())
        {
            Log.Info("Login", "Brak 2FA.");
            if (AuthDiagnostics.IsEnabled)
                await AuthDiagnostics.DumpAsync(_page, "no-2fa-iframe");
            return;
        }

        Log.Info("Login", "Wymagana weryfikacja 2FA.");
        if (AuthDiagnostics.IsEnabled)
            await AuthDiagnostics.DumpAsync(_page, "2fa-iframe-detected");
        await HandleTwoFactorAsync();
    }

    private async Task<bool> WaitForTwoFactorIframeAsync()
    {
        try
        {
            // 2FA iframe może się ładować długo — daj PPO czas.
            await _page.WaitForSelectorAsync(TwoFactorIframeSelector, new PageWaitForSelectorOptions { Timeout = 15_000 });
            return true;
        }
        catch (TimeoutException)
        {
            if (AuthDiagnostics.IsEnabled)
                await AuthDiagnostics.DumpAsync(_page, "2fa-iframe-timeout");
            return false;
        }
    }

    // ─── 2FA handling ──────────────────────────────────────────────────────

    private async Task HandleTwoFactorAsync()
    {
        var frame = _page.Locator(TwoFactorIframeSelector).ContentFrame;
        var codeInputCandidates = new[]
        {
            frame.Locator("#code"),
            frame.Locator("input[name='code']"),
            frame.GetByRole(AriaRole.Textbox, new() { Name = "Wprowadź 6-cyfrowy kod z SMS" }),
            frame.GetByRole(AriaRole.Textbox, new() { Name = "kod z SMS" }),
            frame.GetByRole(AriaRole.Textbox, new() { Name = "kod" }),
            frame.GetByPlaceholder("Wprowadź 6-cyfrowy kod z SMS"),
            frame.Locator("input[autocomplete='one-time-code']"),
            frame.Locator("input[inputmode='numeric']"),
            frame.Locator("input[maxlength='6']"),
            frame.Locator("input[type='tel']"),
            frame.Locator("input[name*='sms' i]"),
            frame.Locator("input[placeholder*='kod' i]"),
        };
        var trustBrowserCandidates = new[]
        {
            frame.Locator("#fprint_state"),
            frame.Locator("input[name='fprint_state']"),
            frame.GetByRole(AriaRole.Checkbox, new() { Name = "Zaufaj tej przeglądarce" }),
            frame.GetByRole(AriaRole.Checkbox, new() { Name = "Zaufaj" }),
            frame.Locator("input[type='checkbox']"),
        };
        var confirmButtonCandidates = new[]
        {
            frame.GetByRole(AriaRole.Button, new() { Name = "SUBMIT THE CODE" }),
            frame.GetByRole(AriaRole.Button, new() { Name = "Submit the code" }),
            frame.GetByRole(AriaRole.Button, new() { Name = "ZATWIERDŹ" }),
            frame.GetByRole(AriaRole.Button, new() { Name = "Zatwierdź" }),
            frame.GetByRole(AriaRole.Button, new() { Name = "Potwierdź" }),
            frame.Locator("button[type='submit']"),
            frame.Locator("button:has-text('SUBMIT THE CODE')"),
            frame.Locator("button:has-text('Submit the code')"),
            frame.Locator("button:has-text('ZATWIERDŹ')"),
            frame.Locator("button:has-text('Zatwierdź')"),
            frame.Locator("button:has-text('Potwierdź')"),
        };

        // Poczekaj aż formularz SMS będzie faktycznie gotowy (nie tylko sam iframe w DOM).
        // Jeśli pole nie pojawi się w 15s, mogą być dwa powody:
        //   (a) zaufana przeglądarka zatwierdziła logowanie automatycznie — iframe zniknął,
        //   (b) PPO używa innej metody (np. push do aplikacji mobilnej).
        try
        {
            var detectedInput = await WaitForAnyVisibleLocatorAsync(codeInputCandidates, TimeSpan.FromSeconds(15));
            if (detectedInput is null)
            {
                await AuthDiagnostics.DumpAsync(_page, "sms-input-not-visible");
                throw new TimeoutException("Nie znaleziono widocznego pola kodu SMS.");
            }

            var codeInput = detectedInput;

            if (AuthDiagnostics.IsEnabled)
                await AuthDiagnostics.DumpAsync(_page, "sms-input-visible");

            var trustBrowser = await WaitForAnyVisibleLocatorAsync(trustBrowserCandidates, TimeSpan.FromSeconds(2));
            var confirmButton = await WaitForAnyVisibleLocatorAsync(confirmButtonCandidates, TimeSpan.FromSeconds(5));

            if (confirmButton is null)
            {
                await AuthDiagnostics.DumpAsync(_page, "confirm-button-not-visible");
                throw new TimeoutException("Nie znaleziono przycisku zatwierdzenia 2FA.");
            }

            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var smsCode = ReadSmsCodeFromConsole(attempt, maxAttempts);

                try
                {
                    await codeInput.ClickAsync();
                    await _page.WaitForTimeoutAsync(150);
                    await codeInput.FillAsync(smsCode);
                    await _page.WaitForTimeoutAsync(150);
                    if (trustBrowser is not null)
                    {
                        try
                        {
                            await trustBrowser.CheckAsync();
                        }
                        catch (PlaywrightException)
                        {
                            // Checkbox bywa niestandardowy lub chwilowo zablokowany — nie blokuj logowania.
                        }
                        await _page.WaitForTimeoutAsync(150);
                    }
                    await confirmButton.ClickAsync();
                }
                catch (TimeoutException ex)
                {
                    Log.Warn("2FA", $"Element nie był dostępny: {ex.Message}. Ponawiam formularz…");
                    continue;
                }
                catch (PlaywrightException ex)
                {
                    Log.Warn("2FA", $"Playwright nie zdążył: {ex.Message}. Ponawiam formularz…");
                    continue;
                }

                if (await WaitForTwoFactorResultAsync(codeInput, TimeSpan.FromSeconds(10)))
                    return;

                if (attempt == maxAttempts)
                    Log.Warn("2FA", "Przekroczono limit prób weryfikacji 2FA.");
                else
                    Log.Warn("2FA", "Nieprawidłowy kod – spróbuj jeszcze raz.");
            }
        }
        catch (TimeoutException)
        {
            if (!await IsTwoFactorIframeStillPresentAsync())
            {
                Log.Info("Login", "2FA zatwierdzone automatycznie (zaufana przeglądarka).");
                return;
            }

            Log.Info("2FA", "Pole SMS nie pojawiło się — czekam na zatwierdzenie innym sposobem (np. push w aplikacji, do 2 min)…");
            await AuthDiagnostics.DumpAsync(_page, "push-wait-fallback");
            await WaitForTwoFactorToVanishAsync(TimeSpan.FromMinutes(2));
        }
    }

    private static async Task<ILocator?> WaitForAnyVisibleLocatorAsync(IEnumerable<ILocator> locators, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var locator in locators)
            {
                try
                {
                    if (await locator.IsVisibleAsync())
                        return locator;
                }
                catch (PlaywrightException)
                {
                    // Iframe/DOM może się przebudowywać podczas challenge'u 2FA.
                }
            }

            await Task.Delay(250);
        }

        return null;
    }

    /// <summary>
    /// Po kliknięciu "Zatwierdź" iframe znika (sukces) lub input jest dalej widoczny (błąd).
    /// Zamiast ślepego Task.Delay, czekaj w pętli do deadline'u — działa niezależnie od szybkości sieci.
    /// </summary>
    private static async Task<bool> WaitForTwoFactorResultAsync(ILocator codeInput, TimeSpan maxWait)
    {
        var deadline = DateTime.UtcNow + maxWait;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(300);
            try
            {
                if (!await codeInput.IsVisibleAsync())
                    return true;
            }
            catch (PlaywrightException)
            {
                // frame odmontowany = sukces (iframe zniknął po zatwierdzeniu)
                return true;
            }
        }
        return false;
    }

    private async Task<bool> IsTwoFactorIframeStillPresentAsync()
    {
        try
        {
            return await _page.Locator(TwoFactorIframeSelector).IsVisibleAsync();
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }

    /// <summary>Czeka aż iframe 2FA zniknie (np. po zatwierdzeniu z telefonu). Ignoruje timeout.</summary>
    private async Task WaitForTwoFactorToVanishAsync(TimeSpan maxWait)
    {
        var deadline = DateTime.UtcNow + maxWait;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(500);
            if (!await IsTwoFactorIframeStillPresentAsync())
                return;
        }
        Log.Warn("2FA", "Minął czas oczekiwania — być może logowanie nie zostało zatwierdzone. Sprawdzę dalej co się stało.");
    }

    private static string ReadSmsCodeFromConsole(int attempt, int maxAttempts)
    {
        while (true)
        {
            Log.Info("2FA", $"Podaj 6-cyfrowy kod z SMS (próba {attempt}/{maxAttempts}):");
            Console.Out.Flush();
            var raw = Console.ReadLine() ?? string.Empty;
            var normalized = string.Concat(raw.Where(c => !char.IsWhiteSpace(c)));

            if (normalized.Length == 6
                && normalized.All(char.IsDigit)
                && int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return normalized;

            Log.Warn("2FA", "Kod musi być 6-cyfrową liczbą (możesz użyć spacji). Spróbuj ponownie.");
        }
    }
}
