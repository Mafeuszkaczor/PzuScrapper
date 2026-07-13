using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace PzuScrapper.Auth;

/// <summary>
/// Wymusza polski język i region w Playwright — niezależnie od locale systemu (np. Mac poza PL).
/// </summary>
internal static class PolishBrowserProfile
{
    public const string Locale = "pl-PL";
    public const string Timezone = "Europe/Warsaw";
    public const string AcceptLanguage = "pl-PL,pl;q=1.0";
    public const float WarsawLatitude = 52.2297f;
    public const float WarsawLongitude = 21.0122f;

    private static readonly (string Key, string Value)[] StorageLanguagePairs =
    [
        ("lang", "pl"),
        ("language", "pl"),
        ("locale", Locale),
        ("i18nextLng", "pl"),
        ("selectedLanguage", "pl"),
        ("selectedLocale", Locale),
    ];

    public static string[] ChromiumLaunchArgs =>
    [
        $"--lang={Locale}",
        "--accept-lang=pl-PL,pl",
        "--disable-blink-features=AutomationControlled",
    ];

    public static string InitScript => """
        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
        Object.defineProperty(navigator, 'language', { get: () => 'pl-PL' });
        Object.defineProperty(navigator, 'languages', { get: () => ['pl-PL', 'pl'] });
        Object.defineProperty(navigator, 'plugins', {
            get: () => [
                { name: 'PDF Viewer' },
                { name: 'Chrome PDF Viewer' },
                { name: 'Chromium PDF Viewer' },
                { name: 'Microsoft Edge PDF Viewer' },
                { name: 'WebKit built-in PDF' },
            ],
        });
        window.chrome = window.chrome || { runtime: {} };
        const originalQuery = window.navigator.permissions && window.navigator.permissions.query;
        if (originalQuery) {
            window.navigator.permissions.query = (parameters) =>
                parameters.name === 'notifications'
                    ? Promise.resolve({ state: Notification.permission })
                    : originalQuery(parameters);
        }
        (() => {
            const pairs = [
                ['lang', 'pl'], ['language', 'pl'], ['locale', 'pl-PL'],
                ['i18nextLng', 'pl'], ['selectedLanguage', 'pl'], ['selectedLocale', 'pl-PL'],
            ];
            const apply = (storage) => {
                if (!storage) return;
                for (const [k, v] of pairs) {
                    try { storage.setItem(k, v); } catch (_) {}
                }
            };
            apply(window.localStorage);
            apply(window.sessionStorage);
        })();
        """;

    public static void ApplyToContextOptions(BrowserNewContextOptions options)
    {
        options.Locale = Locale;
        options.TimezoneId = Timezone;
        options.Geolocation = new Geolocation { Latitude = WarsawLatitude, Longitude = WarsawLongitude };
        options.Permissions = ["geolocation"];

        var extraHeaders = options.ExtraHTTPHeaders?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        extraHeaders["Accept-Language"] = AcceptLanguage;
        options.ExtraHTTPHeaders = extraHeaders;
    }

    /// <summary>
    /// Nadpisuje język w zapisanej sesji — stary browser-state mógł mieć EN w localStorage.
    /// </summary>
    public static string PrepareStorageStatePath(string path)
    {
        if (!File.Exists(path))
            return path;

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root?["origins"] is not JsonArray origins)
                return path;

            var changed = false;
            foreach (var originNode in origins)
            {
                if (originNode is not JsonObject origin)
                    continue;

                var originUrl = origin["origin"]?.GetValue<string>();
                if (originUrl is null || !originUrl.Contains("pzu.pl", StringComparison.OrdinalIgnoreCase))
                    continue;

                var storage = origin["localStorage"] as JsonArray ?? new JsonArray();
                origin["localStorage"] = UpsertPolishLanguageStorage(storage);
                changed = true;
            }

            if (!changed)
                return path;

            var sanitized = Path.Combine(
                Path.GetTempPath(),
                $"pzuscrapper-state-{Guid.NewGuid():N}.json");
            File.WriteAllText(sanitized, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            return sanitized;
        }
        catch (Exception ex)
        {
            Log.Warn("Session", $"Nie udało się poprawić języka w zapisanej sesji: {ex.Message}");
            return path;
        }
    }

    public static async Task ApplyToContextAsync(IBrowserContext context)
    {
        await context.AddInitScriptAsync(InitScript);
        await TrySetLanguageCookiesAsync(context);
        await context.RouteAsync("**/*", async route =>
        {
            var headers = route.Request.Headers.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase);
            headers["Accept-Language"] = AcceptLanguage;
            await route.ContinueAsync(new RouteContinueOptions { Headers = headers });
        });
    }

    /// <summary>
    /// Po pierwszym wejściu na PZU: ustaw storage i przeładuj, jeśli UI nie jest po polsku.
    /// </summary>
    public static async Task EnsurePolishUiAsync(IPage page, int navigationTimeoutMs)
    {
        await OverridePageStorageLanguageAsync(page);

        if (await HasPolishHomeUiAsync(page))
            return;

        Log.Info("Auth", "UI nie jest po polsku — wymuszam język i przeładowuję stronę…");
        await OverridePageStorageLanguageAsync(page);
        await page.ReloadAsync(new PageReloadOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = navigationTimeoutMs,
        });
        await OverridePageStorageLanguageAsync(page);
    }

    private static async Task<bool> HasPolishHomeUiAsync(IPage page)
    {
        var polishMarkers = new[]
        {
            page.Locator("a").Filter(new() { HasText = "Moje konto" }),
            page.GetByRole(AriaRole.Button, new() { Name = "Akceptuj wszystkie" }),
            page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj" }),
        };

        foreach (var marker in polishMarkers)
        {
            try
            {
                if (await marker.First.IsVisibleAsync())
                    return true;
            }
            catch (PlaywrightException)
            {
            }
        }

        return false;
    }

    private static Task OverridePageStorageLanguageAsync(IPage page) =>
        page.EvaluateAsync(@"() => {
            const pairs = [
                ['lang', 'pl'], ['language', 'pl'], ['locale', 'pl-PL'],
                ['i18nextLng', 'pl'], ['selectedLanguage', 'pl'], ['selectedLocale', 'pl-PL'],
            ];
            const apply = (storage) => {
                if (!storage) return;
                for (const [k, v] of pairs) {
                    try { storage.setItem(k, v); } catch (_) {}
                }
            };
            apply(window.localStorage);
            apply(window.sessionStorage);
            if (document.documentElement) {
                document.documentElement.lang = 'pl-PL';
            }
        }");

    private static JsonArray UpsertPolishLanguageStorage(JsonArray storage)
    {
        var byName = storage
            .OfType<JsonObject>()
            .Where(o => o["name"]?.GetValue<string>() is not null)
            .ToDictionary(o => o["name"]!.GetValue<string>()!, o => o, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in StorageLanguagePairs)
        {
            if (byName.TryGetValue(key, out var existing))
                existing["value"] = value;
            else
                storage.Add(new JsonObject { ["name"] = key, ["value"] = value });
        }

        foreach (var entry in storage.OfType<JsonObject>().ToList())
        {
            var name = entry["name"]?.GetValue<string>();
            var value = entry["value"]?.GetValue<string>();
            if (name is null || value is null)
                continue;

            if (name.Contains("lang", StringComparison.OrdinalIgnoreCase)
                && (value.Equals("en", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("en-", StringComparison.OrdinalIgnoreCase)))
            {
                entry["value"] = name.Contains("locale", StringComparison.OrdinalIgnoreCase) ? Locale : "pl";
            }
        }

        return storage;
    }

    private static async Task TrySetLanguageCookiesAsync(IBrowserContext context)
    {
        try
        {
            await context.AddCookiesAsync(
            [
                new Cookie { Name = "lang", Value = "pl", Domain = "ppo.pzu.pl", Path = "/", Secure = true, SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "language", Value = "pl", Domain = "ppo.pzu.pl", Path = "/", Secure = true, SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "locale", Value = Locale, Domain = "ppo.pzu.pl", Path = "/", Secure = true, SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "lang", Value = "pl", Domain = ".pzu.pl", Path = "/", Secure = true, SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "language", Value = "pl", Domain = ".pzu.pl", Path = "/", Secure = true, SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "locale", Value = Locale, Domain = ".pzu.pl", Path = "/", Secure = true, SameSite = SameSiteAttribute.Lax },
            ]);
        }
        catch
        {
            // best effort
        }
    }
}
