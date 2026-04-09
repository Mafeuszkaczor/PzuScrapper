using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Models;

namespace PzuScrapper.Auctions;

internal sealed class AuctionSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(), new UtcIsoDateTimeConverter() },
    };

    private readonly HttpClient _http;
    private readonly BidderSearchFilters? _filters;

    public AuctionSearchService(HttpClient http, BidderSearchFilters? filters = null)
    {
        _http = http;
        _filters = filters;
    }

    /// <param name="alreadyHaveAuctionNumbers">Auction IDs already in CSV (skipped); pagination stops early if a full page is duplicates.</param>
    public async Task<List<Car>> SearchAllCarsAsync(ISet<string>? alreadyHaveAuctionNumbers = null)
    {
        var allCars = new List<Car>();
        var seenThisRun = new HashSet<string>(StringComparer.Ordinal);
        var currentPage = 0;
        int totalPages;

        do
        {
            var request = BidderSearchRequestFactory.Create(currentPage, _filters);
            var json = JsonSerializer.Serialize(request, RequestJsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            SearchResponse? searchResponse;
            try
            {
                using var response = await _http.PostAsync("/api/auction/auction/search/bidder", content);
                if (!response.IsSuccessStatusCode)
                {
                    FinishListFetchProgressLine();
                    Console.WriteLine(
                        $"[Scrape] Serwer zwrócił błąd (kod {(int)response.StatusCode}). "
                        + $"Zatrzymuję pobieranie — zebrano już {allCars.Count} pozycji.");
                    return allCars;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                searchResponse = JsonSerializer.Deserialize<SearchResponse>(responseJson, JsonOptions);
            }
            catch (HttpRequestException ex)
            {
                FinishListFetchProgressLine();
                Console.WriteLine(
                    $"[Scrape] Problem z połączeniem sieciowym. Zatrzymuję pobieranie — zebrano {allCars.Count} pozycji. ({ex.Message})");
                return allCars;
            }
            catch (TaskCanceledException)
            {
                FinishListFetchProgressLine();
                Console.WriteLine(
                    $"[Scrape] Przekroczono czas oczekiwania na odpowiedź. Zatrzymuję pobieranie — zebrano {allCars.Count} pozycji.");
                return allCars;
            }
            catch (JsonException)
            {
                FinishListFetchProgressLine();
                Console.WriteLine(
                    $"[Scrape] Nie udało się odczytać odpowiedzi serwera. Zatrzymuję pobieranie — zebrano {allCars.Count} pozycji.");
                return allCars;
            }

            var pageRows = searchResponse?.result;
            var pageCount = pageRows?.Count ?? 0;
            var addedBeforePage = allCars.Count;

            if (pageRows is { Count: > 0 })
            {
                foreach (var car in pageRows)
                {
                    var id = car.auctionUniqueNumber?.Trim();
                    if (string.IsNullOrEmpty(id))
                        continue;
                    if (alreadyHaveAuctionNumbers != null && alreadyHaveAuctionNumbers.Contains(id))
                        continue;
                    if (!seenThisRun.Add(id))
                        continue;
                    allCars.Add(car);
                }

                if (alreadyHaveAuctionNumbers != null)
                {
                    var withId = pageRows.Where(c => !string.IsNullOrWhiteSpace(c.auctionUniqueNumber)).ToList();
                    if (withId.Count > 0
                        && withId.All(c => alreadyHaveAuctionNumbers.Contains(c.auctionUniqueNumber!.Trim())))
                    {
                        FinishListFetchProgressLine();
                        Console.WriteLine(
                            "[Scrape] Wszystkie pozycje w tej partii wyników są już zapisane – kończę pobieranie listy.");
                        return allCars;
                    }
                }
            }

            totalPages = searchResponse?.totalPages ?? 1;
            var newThisPage = allCars.Count - addedBeforePage;
            WriteListFetchProgressLine(pageCount, newThisPage, allCars.Count);

            currentPage++;
        }
        while (currentPage < totalPages);

        FinishListFetchProgressLine();
        return allCars;
    }

    /// <summary>Single console line overwritten on each list response (progress).</summary>
    private static void WriteListFetchProgressLine(int pageCount, int newThisPage, int totalCollected)
    {
        var line =
            $"[Scrape] Pobrano {pageCount} pozycji, nowych w tej partii: {newThisPage} (łącznie zebranych: {totalCollected}).";
        var width = Console.WindowWidth;
        if (width < 8)
            width = 120;
        var pad = Math.Max(0, width - 1 - line.Length);
        Console.Write($"\r{line}{new string(' ', pad)}");
    }

    private static void FinishListFetchProgressLine() => Console.WriteLine();
}
