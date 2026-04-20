using PzuScrapper.Models.Car;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PzuScrapper.Auctions
{
    public class CarDataQueryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        public HttpClient _client { get; set; }

        public CarDataQueryService(HttpClient httpClient)
        {
            _client = httpClient;
        }

        public async Task<CarDetails?> GetCarDataAsync(string CarId)
        {
            try
            {
                if (!int.TryParse(CarId, out var parsedId))
                {
                    Console.WriteLine($"\n[Scrape] Nieprawidłowy format ID pojazdu. Zatrzymuję pobieranie. {CarId}");
                    return null;
                }

                using var response = await _client.GetAsync($"/api/auction/vehicle/sale/{parsedId}/bidder");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine(
                        $"\n[Scrape] Serwer zwrócił błąd (kod {(int)response.StatusCode}). "
                        + $"Zatrzymuję pobieranie");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var car = JsonSerializer.Deserialize<CarDetails>(responseJson, JsonOptions);
                return car;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(
                    $"\n[Scrape] Problem z połączeniem sieciowym. Zatrzymuję pobieranie — {ex.Message}");
                return null;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine(
                    $"\n[Scrape] Przekroczono czas oczekiwania na odpowiedź. Zatrzymuję pobieranie.");
                return null;
            }
            catch (JsonException)
            {
                Console.WriteLine(
                    $"\n[Scrape] Nie udało się odczytać odpowiedzi serwera. Zatrzymuję pobieranie.");
                return null;
            }
        }
    }
}
