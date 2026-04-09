using Microsoft.Playwright;

namespace PzuScrapper.Api;

/// <summary>Reads Bearer token from the page sessionStorage (after login).</summary>
internal static class SessionTokenReader
{
    public static async Task<string?> ReadAsync(IPage page)
    {
        var token = await page.EvaluateAsync<string?>(@"() => {
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

        if (token == null)
            Console.WriteLine(
                "[Scrape] Ostrzeżenie: nie udało się odczytać uprawnień z przeglądarki. Lista ofert może być niepełna.");
        else
            Console.WriteLine("[Scrape] Połączenie z listą ofert gotowe.");

        return token;
    }
}
