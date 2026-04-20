using System.Net.Http;
using Microsoft.Playwright;
using Models;
using PzuScrapper.Configuration;

namespace PzuScrapper.Scraping;

public sealed class CarPhotoScraper
{
    private const string CounterSelector = ".image-large__counter-text";
    private const string NextArrowSelector = "img[src*='next-arrow']";

    /// <summary>Katalog roboczy zdjęć dla pojazdu (VIN lub numer oferty).</summary>
    public static string ResolvePhotoDirectory(Car car)
    {
        var vin = car.serialNumber?.ToString();
        var folderName = !string.IsNullOrWhiteSpace(vin) ? vin : car.auctionUniqueNumber ?? "unknown";
        return Path.Combine(AutaCsvPaths.PhotosDirectory, folderName);
    }

    public async Task GetPhotosAsync(IPage page, Car car, HttpClient http)
    {
        var vin = car.serialNumber?.ToString();
        var folderName = !string.IsNullOrWhiteSpace(vin) ? vin : car.auctionUniqueNumber ?? "unknown";
        var label = folderName;

        var outputDir = ResolvePhotoDirectory(car);
        Directory.CreateDirectory(outputDir);

        try
        {
            await page.WaitForSelectorAsync(CounterSelector, new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        catch
        {
            Console.WriteLine($"  [{label}] Brak galerii zdjęć – pomijam.");
            return;
        }

        var counterText = await page.Locator(CounterSelector).InnerTextAsync();
        var totalPhotos = ParseTotalFromCounter(counterText);
        Console.WriteLine($"  [{label}] W galerii: {totalPhotos} zdjęć.");

        var missingCount = CountMissingPhotos(outputDir, totalPhotos);
        if (missingCount == 0)
        {
            Console.WriteLine($"  [{label}] Komplet ({totalPhotos} szt., foto_001.jpg – foto_{totalPhotos:000}.jpg) – pomijam galerię.");
            return;
        }

        Console.WriteLine($"  [{label}] Brakuje {missingCount}/{totalPhotos} zdjęć – przechodzę galerię i uzupełniam.");

        for (var i = 0; i < totalPhotos; i++)
        {
            var filePath = Path.Combine(outputDir, $"foto_{i + 1:D3}.jpg");
            var imgSrc = await page
                .GetByRole(AriaRole.Img, new() { Name = "image-large" })
                .GetAttributeAsync("src");

            if (!string.IsNullOrEmpty(imgSrc))
            {
                if (File.Exists(filePath))
                    Console.WriteLine($"  [{label}] {i + 1}/{totalPhotos} – już zapisane, przechodzę dalej.");
                else
                {
                    await DownloadImageAsync(page, http, imgSrc, filePath, label, i + 1, totalPhotos);
                    if (File.Exists(filePath))
                        Console.WriteLine($"  [{label}] {i + 1}/{totalPhotos} – zapisano.");
                    else
                        Console.WriteLine($"  [{label}] {i + 1}/{totalPhotos} – nie udało się zapisać.");
                }
            }
            else
                Console.WriteLine($"  [{label}] {i + 1}/{totalPhotos} – brak podglądu, pomijam.");

            if (i < totalPhotos - 1)
            {
                await page.Locator(NextArrowSelector).First.ClickAsync();

                var expectedIndex = i + 2;
                await page.WaitForFunctionAsync(
                    @"(idx) => {
                            const el = document.querySelector('.image-large__counter-text');
                            return el && el.innerText.trim().startsWith(idx + ' ');
                        }",
                    arg: expectedIndex,
                    new PageWaitForFunctionOptions { Timeout = 10_000 }
                );
            }
        }
    }

    /// <summary>How many of foto_001…foto_N are missing vs. the count reported by the page.</summary>
    private static int CountMissingPhotos(string outputDir, int totalPhotos)
    {
        var n = 0;
        for (var j = 1; j <= totalPhotos; j++)
        {
            var p = Path.Combine(outputDir, $"foto_{j:D3}.jpg");
            if (!File.Exists(p))
                n++;
        }

        return n;
    }

    private static int ParseTotalFromCounter(string counterText)
    {
        var parts = counterText.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var total))
            return total;
        return 1;
    }

    /// <summary>blob: only in browser (fetch + FileReader); http(s) via HttpClient.</summary>
    private static async Task DownloadImageAsync(
        IPage page,
        HttpClient http,
        string imgSrc,
        string filePath,
        string logLabel,
        int photoIndex,
        int photoTotal)
    {
        if (File.Exists(filePath))
            return;

        try
        {
            byte[] bytes;
            if (imgSrc.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                var b64 = await page.EvaluateAsync<string>(
                    @"async (blobUrl) => {
                        const r = await fetch(blobUrl);
                        const blob = await r.blob();
                        return await new Promise((resolve, reject) => {
                            const reader = new FileReader();
                            reader.onloadend = () => {
                                const s = reader.result;
                                resolve(s.substring(s.indexOf(',') + 1));
                            };
                            reader.onerror = () => reject(reader.error);
                            reader.readAsDataURL(blob);
                        });
                    }",
                    imgSrc);
                if (string.IsNullOrEmpty(b64))
                    throw new InvalidOperationException("Empty blob decode result (base64).");
                bytes = Convert.FromBase64String(b64);
            }
            else
                bytes = await http.GetByteArrayAsync(imgSrc);

            await File.WriteAllBytesAsync(filePath, bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [{logLabel}] Błąd zapisu zdjęcia {photoIndex}/{photoTotal}: {ex.Message}");
        }
    }
}
