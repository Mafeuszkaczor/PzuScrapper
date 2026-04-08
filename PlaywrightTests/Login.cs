using Microsoft.Playwright.MSTest;
using Microsoft.Playwright;
using PzuScrapper.Models;

namespace PlaywrightTests
{
    [TestClass]
    public class Login
    {
        private IPage _page { get; set; }
        private SiteSession _siteSession { get; set; }

        public Login(IPage page, SiteSession siteSession)
        {
            _page = page;
            _siteSession = siteSession;
        }

        [TestMethod]
        public async Task<IPage> LoginAsync()
        {
            Console.WriteLine("[Login] Loguję...");
            await _page.GotoAsync("https://ppo.pzu.pl/");
            await _page.Locator("body").ClickAsync();

            var acceptCookies = _page.GetByRole(AriaRole.Button, new() { Name = "Akceptuj wszystkie" });
            if (await acceptCookies.IsVisibleAsync())
                await acceptCookies.ClickAsync();

            await _page.Locator("a").Filter(new() { HasText = "Moje konto" }).ClickAsync();
            await _page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj" }).ClickAsync();
            await _page.GetByRole(AriaRole.Textbox, new() { Name = "Użytkownik" }).ClickAsync();
            await _page.GetByRole(AriaRole.Textbox, new() { Name = "Użytkownik" }).FillAsync(_siteSession.Login);
            await _page.GetByRole(AriaRole.Textbox, new() { Name = "Hasło" }).ClickAsync();
            await _page.GetByRole(AriaRole.Textbox, new() { Name = "Hasło" }).FillAsync(_siteSession.Password);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj" }).ClickAsync();

            // Czekamy na iframe 2FA lub pomijamy jeśli nie pojawi się w ciągu 5s
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

            Console.WriteLine("[Login] Logowanie zakończone.");

            return _page;
        }

        [TestMethod]
        public async Task<IPage> TwoFactorAuthenticationAsync(IPage page)
        {
            var frame = page.Locator("#secfense_iframe").ContentFrame;
            var codeInput = frame.GetByRole(AriaRole.Textbox, new() { Name = "Wprowadź 6-cyfrowy kod z SMS" });

            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                Console.WriteLine($"[2FA] Podaj 6-cyfrowy kod z SMS (próba {attempt}/{maxAttempts}):");
                Console.Out.Flush();
                var smsCode = Console.ReadLine() ?? string.Empty;

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
                    Console.WriteLine($"Nieprawidłowy kod – spróbuj jeszcze raz.");
            }

            return page;
        }
    }
}

