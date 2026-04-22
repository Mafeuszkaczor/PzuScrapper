using Microsoft.Playwright;
using PzuScrapper.Configuration;
using PzuScrapper.Models;

namespace PzuScrapper.Scraping;

public sealed class CarPhotoScraper
{
    private const string CounterSelector = ".image-large__counter-text";
    private const string NextArrowSelector = "img[src*='next-arrow']";

    /// <summary>Working photo folder for a vehicle (VIN or auction number).</summary>
    public static string ResolvePhotoDirectory(Car car)
    {
        return Path.Combine(AppPaths.PhotosDirectory, ResolveFolderName(car));
    }

    public async Task GetPhotosAsync(IPage page, Car car, HttpClient http)
    {
        var folderName = ResolveFolderName(car);
        var outputDir = ResolvePhotoDirectory(car);
        Directory.CreateDirectory(outputDir);

        try
        {
            await page.WaitForSelectorAsync(CounterSelector, new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        catch
        {
            Console.WriteLine($"  [{folderName}] Brak galerii zdjęć – pomijam.");
            return;
        }

        var counterText = await page.Locator(CounterSelector).InnerTextAsync();
        var totalPhotos = ParseTotalFromCounter(counterText);
        var missingCount = CountMissingPhotos(outputDir, totalPhotos);

        if (missingCount == 0)
        {
            Console.WriteLine($"  [{folderName}] Komplet ({totalPhotos} szt.) – pomijam galerię.");
            return;
        }

        Console.WriteLine($"  [{folderName}] W galerii: {totalPhotos} zdjęć, brakuje {missingCount} – uzupełniam.");

        var progress = new PhotoProgress(folderName, totalPhotos);

        for (var i = 0; i < totalPhotos; i++)
        {
            var filePath = Path.Combine(outputDir, $"foto_{i + 1:D3}.jpg");
            var imgSrc = await page
                .GetByRole(AriaRole.Img, new() { Name = "image-large" })
                .GetAttributeAsync("src");

            if (string.IsNullOrEmpty(imgSrc))
                progress.OnSkipped(i + 1, "brak podglądu");
            else if (File.Exists(filePath))
                progress.OnAlreadySaved(i + 1);
            else
            {
                var err = await DownloadImageAsync(page, http, imgSrc, filePath);
                if (err is null && File.Exists(filePath))
                    progress.OnSaved(i + 1);
                else
                    progress.OnError(i + 1, err ?? "nie udało się zapisać");
            }

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
                    new PageWaitForFunctionOptions { Timeout = 10_000 });
            }
        }

        progress.Finish();
    }

    private static string ResolveFolderName(Car car)
    {
        var vin = car.SerialNumber?.ToString();
        return !string.IsNullOrWhiteSpace(vin) ? vin : car.AuctionUniqueNumber ?? "unknown";
    }

    private static int CountMissingPhotos(string outputDir, int totalPhotos)
    {
        var n = 0;
        for (var j = 1; j <= totalPhotos; j++)
            if (!File.Exists(Path.Combine(outputDir, $"foto_{j:D3}.jpg")))
                n++;
        return n;
    }

    private static int ParseTotalFromCounter(string counterText)
    {
        var parts = counterText.Split('/');
        return parts.Length == 2 && int.TryParse(parts[1].Trim(), out var total) ? total : 1;
    }

    /// <summary>
    /// Returns null on success, or an error message on failure (avoids mid-loop prints).
    /// </summary>
    private static async Task<string?> DownloadImageAsync(IPage page, HttpClient http, string imgSrc, string filePath)
    {
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
                    return "pusty blob";
                bytes = Convert.FromBase64String(b64);
            }
            else
                bytes = await http.GetByteArrayAsync(imgSrc);

            await File.WriteAllBytesAsync(filePath, bytes);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ─── Single-line overwriting progress + deferred error summary ──────────

    private sealed class PhotoProgress
    {
        private readonly string _folderName;
        private readonly int _total;
        private readonly List<string> _errors = new();

        private int _saved;
        private int _alreadySaved;
        private int _skipped;
        private int _errorCount;
        private int _lastLineLength;

        public PhotoProgress(string folderName, int total)
        {
            _folderName = folderName;
            _total = total;
        }

        public void OnSaved(int index)        { _saved++;        Render(index, "zapisano"); }
        public void OnAlreadySaved(int index) { _alreadySaved++; Render(index, "już zapisane"); }
        public void OnSkipped(int index, string reason)
        {
            _skipped++;
            Render(index, $"pominięto – {reason}");
        }
        public void OnError(int index, string reason)
        {
            _errorCount++;
            _errors.Add($"  [{_folderName}] Błąd zdjęcia {index}/{_total}: {reason}");
            Render(index, "błąd zapisu");
        }

        public void Finish()
        {
            EraseCurrentLine();
            Console.WriteLine(
                $"  [{_folderName}] Gotowe {_total}/{_total} – zapisane: {_saved}, już były: {_alreadySaved}, pominięte: {_skipped}, błędy: {_errorCount}.");
            foreach (var line in _errors)
                Console.WriteLine(line);
        }

        private void Render(int index, string status)
        {
            var line =
                $"  [{_folderName}] {index}/{_total} – {status} " +
                $"(zapisane: {_saved}, już były: {_alreadySaved}, pominięte: {_skipped}, błędy: {_errorCount})";

            var padding = Math.Max(0, _lastLineLength - line.Length);
            Console.Write('\r' + line + new string(' ', padding));
            _lastLineLength = line.Length;
        }

        private void EraseCurrentLine()
        {
            if (_lastLineLength == 0) return;
            Console.Write('\r' + new string(' ', _lastLineLength) + '\r');
            _lastLineLength = 0;
        }
    }
}
