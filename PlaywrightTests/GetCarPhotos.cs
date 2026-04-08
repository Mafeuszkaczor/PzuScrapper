using System.Net.Http;
using Microsoft.Playwright;
using Models;

namespace PlaywrightTests
{
    public class CarPhotoScraper
    {
        public async Task GetPhotosAsync(IPage page, Car car, HttpClient http)
        {
            var vin = car.serialNumber?.ToString();
            var folderName = !string.IsNullOrWhiteSpace(vin) ? vin : car.auctionUniqueNumber;
            var label = folderName;

            var outputDir = Path.Combine("photos", folderName);
            Directory.CreateDirectory(outputDir);

            try
            {
                await page.WaitForSelectorAsync(".image-gallery__counter", new PageWaitForSelectorOptions { Timeout = 10_000 });
            }
            catch
            {
                Console.WriteLine($"  [{label}] Brak galerii zdjęć – pomijam.");
                return;
            }

            var counterText = await page.Locator(".image-gallery__counter").InnerTextAsync();
            var totalPhotos = ParseTotalFromCounter(counterText);
            Console.WriteLine($"  [{label}] Galeria: {totalPhotos} zdjęć");

            for (int i = 0; i < totalPhotos; i++)
            {
                var imgSrc = await page
                    .GetByRole(AriaRole.Img, new() { Name = "image-large" })
                    .GetAttributeAsync("src");

                if (!string.IsNullOrEmpty(imgSrc))
                {
                    var filePath = Path.Combine(outputDir, $"foto_{i + 1:D3}.jpg");
                    await DownloadImageAsync(http, imgSrc, filePath);
                    Console.WriteLine($"  [{label}] Zdjęcie {i + 1}/{totalPhotos} pobrane.");
                }

                if (i < totalPhotos - 1)
                {
                    await page.Locator(".image-gallery__arrow-right").ClickAsync();

                    var expectedIndex = i + 2;
                    await page.WaitForFunctionAsync(
                        @"(idx) => {
                            const el = document.querySelector('.image-gallery__counter');
                            return el && el.innerText.trim().startsWith(idx + ' ');
                        }",
                        arg: expectedIndex,
                        new PageWaitForFunctionOptions { Timeout = 10_000 }
                    );
                }
            }
        }

        private static int ParseTotalFromCounter(string counterText)
        {
            var parts = counterText.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var total))
                return total;
            return 1;
        }

        private static async Task DownloadImageAsync(HttpClient http, string imgSrc, string filePath)
        {
            if (File.Exists(filePath))
                return;

            try
            {
                var bytes = await http.GetByteArrayAsync(imgSrc);
                await File.WriteAllBytesAsync(filePath, bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Błąd pobierania [{imgSrc}]: {ex.Message}");
            }
        }
    }
}
