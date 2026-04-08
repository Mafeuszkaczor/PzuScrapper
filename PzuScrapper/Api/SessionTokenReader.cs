using Microsoft.Playwright;

namespace PzuScrapper.Api;

/// <summary>Wyciąga Bearer z sessionStorage strony (po zalogowaniu).</summary>
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
            Console.WriteLine("UWAGA: Nie znaleziono tokenu auth w sessionStorage. Żądania API mogą być odrzucone.");
        else
            Console.WriteLine("Token auth wydobyty pomyślnie.");

        return token;
    }
}
