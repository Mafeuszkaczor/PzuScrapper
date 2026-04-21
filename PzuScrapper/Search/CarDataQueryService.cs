using System.Text.Json;
using PzuScrapper.Models;

namespace PzuScrapper.Search;

internal sealed class CarDataQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _client;

    public CarDataQueryService(HttpClient client) => _client = client;

    public async Task<CarDetails?> GetCarDataAsync(string carId, int retries = 2)
    {
        if (!int.TryParse(carId, out var parsedId))
        {
            Console.WriteLine($"[API] Nieprawidłowy format ID pojazdu: {carId}");
            return null;
        }

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromSeconds(attempt * 2);
                Console.WriteLine($"[API] Ponawiam za {delay.TotalSeconds}s (próba {attempt + 1}/{retries + 1})…");
                await Task.Delay(delay);
            }

            try
            {
                using var response = await _client.GetAsync($"/api/auction/vehicle/sale/{parsedId}/bidder");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[API] HTTP {(int)response.StatusCode} dla oferty {carId}.");
                    if ((int)response.StatusCode == 429 && attempt < retries)
                        continue;
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                try
                {
                    return JsonSerializer.Deserialize<CarDetails>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    var snippet = json.Length > 200 ? json[..200] : json;
                    Console.WriteLine($"[API] Błąd parsowania JSON dla {carId}: {ex.Message}");
                    Console.WriteLine($"[API] Odpowiedź (fragment): {snippet}");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[API] Problem sieciowy (próba {attempt + 1}): {ex.Message}");
                if (attempt == retries) return null;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"[API] Timeout dla oferty {carId} (próba {attempt + 1}).");
                if (attempt == retries) return null;
            }
        }

        return null;
    }
}
