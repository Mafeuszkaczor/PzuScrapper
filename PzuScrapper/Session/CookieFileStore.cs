using System.Text.Json;
using Microsoft.Playwright;
using PzuScrapper.Configuration;

namespace PzuScrapper.Session;

internal sealed class CookieFileStore
{
    private readonly string _path;

    public CookieFileStore(string? path = null)
    {
        _path = path ?? LocalDataPaths.Cookies;
    }

    public async Task LoadIntoAsync(IBrowserContext context)
    {
        if (!File.Exists(_path))
            return;

        var json = await File.ReadAllTextAsync(_path);
        var dtos = JsonSerializer.Deserialize<List<CookieJsonDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var withValues = dtos.Where(d => !string.IsNullOrWhiteSpace(d.Value)).ToList();
        if (withValues.Count == 0)
            return;

        var cookies = withValues.Select(ToPlaywrightCookie).ToList();
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
