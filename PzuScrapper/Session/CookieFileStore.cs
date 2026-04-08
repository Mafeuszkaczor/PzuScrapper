using System.Text.Json;
using Microsoft.Playwright;

namespace PzuScrapper.Session;

/// <summary>Odczyt i zapis pliku cookies.json w formacie Playwright.</summary>
internal sealed class CookieFileStore
{
    private readonly string _path;

    public CookieFileStore(string path = "cookies.json")
    {
        _path = path;
    }

    public async Task LoadIntoAsync(IBrowserContext context)
    {
        if (!File.Exists(_path))
            return;

        var json = await File.ReadAllTextAsync(_path);
        var dtos = JsonSerializer.Deserialize<List<CookieJsonDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (dtos is not { Count: > 0 })
            return;

        var cookies = dtos.Select(ToPlaywrightCookie).ToList();
        await context.AddCookiesAsync(cookies);
        Console.WriteLine($"[Session] Wczytano {cookies.Count} cookies.");
    }

    public async Task SaveFromAsync(IBrowserContext context)
    {
        var cookies = await context.CookiesAsync(new[] { "https://ppo.pzu.pl" });
        var json = JsonSerializer.Serialize(cookies, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_path, json);
        Console.WriteLine($"[Session] Zapisano {cookies.Count} cookies.");
    }

    private static Cookie ToPlaywrightCookie(CookieJsonDto d) => new()
    {
        Name     = d.Name,
        Value    = d.Value,
        Domain   = d.Domain,
        Path     = d.Path,
        Expires  = d.Expires,
        HttpOnly = d.HttpOnly,
        Secure   = d.Secure,
        SameSite = d.SameSite switch
        {
            "Strict" => SameSiteAttribute.Strict,
            "Lax"    => SameSiteAttribute.Lax,
            _        => SameSiteAttribute.None,
        },
    };
}

/// <summary>Rekord z cookies.json (nie Playwright.Cookie – SameSite jako string).</summary>
internal sealed class CookieJsonDto
{
    public string Name     { get; set; } = string.Empty;
    public string Value    { get; set; } = string.Empty;
    public string Domain   { get; set; } = string.Empty;
    public string Path     { get; set; } = "/";
    public float  Expires  { get; set; } = -1;
    public bool   HttpOnly { get; set; }
    public bool   Secure   { get; set; }
    public string SameSite { get; set; } = "None";
}
