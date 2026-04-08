using System.Net.Http.Headers;
using Microsoft.Playwright;

namespace PzuScrapper.Api;

/// <summary>HttpClient do API PZU z nagłówkami jak przeglądarka (Bearer + Cookie).</summary>
internal static class PzuHttpClientFactory
{
    private static readonly Uri BaseUri = new("https://ppo.pzu.pl");

    public static async Task<HttpClient> CreateAsync(string? bearerToken, IBrowserContext context)
    {
        var handler = new HttpClientHandler { UseCookies = false };
        var http = new HttpClient(handler) { BaseAddress = BaseUri };

        if (!string.IsNullOrEmpty(bearerToken))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var cookies = await context.CookiesAsync(new[] { "https://ppo.pzu.pl" });
        if (cookies.Count > 0)
        {
            var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
            http.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }

        return http;
    }
}
