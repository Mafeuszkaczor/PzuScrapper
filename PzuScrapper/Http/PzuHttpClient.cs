using System.Net.Http.Headers;
using Microsoft.Playwright;
using PzuScrapper.Auth;

namespace PzuScrapper.Http;

// ─── Baggage header capture ────────────────────────────────────────────────

/// <summary>
/// Subscribes to page requests BEFORE auth navigation and captures the
/// baggage-user-uuid header sent by the PZU SPA with every authenticated API call.
/// Register before any navigation; dispose after the header is captured.
/// </summary>
internal sealed class BaggageHeaderCapture : IDisposable
{
    private readonly IPage _page;
    private readonly TaskCompletionSource<string> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BaggageHeaderCapture(IPage page)
    {
        _page = page;
        _page.Request += OnRequest;
    }

    /// <summary>Waits up to <paramref name="timeout"/> for the header. Returns null on timeout.</summary>
    public async Task<string?> WaitAsync(TimeSpan timeout)
    {
        var winner = await Task.WhenAny(_tcs.Task, Task.Delay(timeout));
        return winner == _tcs.Task ? _tcs.Task.Result : null;
    }

    private void OnRequest(object? _, IRequest req)
    {
        if (!req.Url.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            return;
        if (req.Headers.TryGetValue("baggage-user-uuid", out var value) &&
            !string.IsNullOrWhiteSpace(value))
            _tcs.TrySetResult(value);
    }

    public void Dispose() => _page.Request -= OnRequest;
}

// ─── HTTP client factory ───────────────────────────────────────────────────

/// <summary>
/// Builds an HttpClient for ppo.pzu.pl with Bearer token, optional baggage header,
/// and browser cookies copied from the Playwright context.
/// Also reads the JWT token from page sessionStorage (was SessionTokenReader).
/// </summary>
internal static class PzuHttpClient
{
    private static readonly Uri BaseUri = new("https://ppo.pzu.pl");

    public static async Task<HttpClient> CreateAsync(
        IPage page,
        IBrowserContext context,
        string? userUuid = null)
    {
        var bearerToken = await ReadTokenFromPageAsync(page);
        if (bearerToken is null)
            Log.Warn("Scrape", "nie udało się odczytać uprawnień z przeglądarki. Lista ofert może być niepełna.");
        else
            Log.Info("Scrape", "Połączenie z listą ofert gotowe.");

        var handler = new HttpClientHandler { UseCookies = false };
        var http = new HttpClient(handler) { BaseAddress = BaseUri };

        if (!string.IsNullOrEmpty(bearerToken))
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);

        if (!string.IsNullOrEmpty(userUuid))
            http.DefaultRequestHeaders.Add("baggage-user-uuid", userUuid);

        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(PolishBrowserProfile.AcceptLanguage);

        await ApplyCookiesAsync(http, context);

        return http;
    }

    /// <summary>
    /// Re-reads token and cookies from the live browser context and updates the client headers.
    /// Called before every authenticated request; intentionally silent.
    /// </summary>
    public static async Task RefreshAsync(HttpClient http, IPage page, IBrowserContext context)
    {
        var bearerToken = await ReadTokenFromPageAsync(page);
        if (!string.IsNullOrEmpty(bearerToken))
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);

        http.DefaultRequestHeaders.Remove("Cookie");
        await ApplyCookiesAsync(http, context);
    }

    private static async Task ApplyCookiesAsync(HttpClient http, IBrowserContext context)
    {
        var cookies = await context.CookiesAsync(new[] { "https://ppo.pzu.pl" });
        if (cookies.Count > 0)
        {
            var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
            http.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }
    }

    private static Task<string?> ReadTokenFromPageAsync(IPage page) =>
        page.EvaluateAsync<string?>(@"() => {
            const knownKeys = ['token', 'refresh'];
            for (const key of knownKeys) {
                const val = sessionStorage.getItem(key);
                if (val) return val;
            }
            for (let i = 0; i < sessionStorage.length; i++) {
                const k = sessionStorage.key(i);
                const v = sessionStorage.getItem(k);
                if (v && v.startsWith('eyJ')) return v;
            }
            return null;
        }");
}
