using System.Text;
using System.Text.Json;
using PzuScrapper.Configuration;
using PzuScrapper.Models;

namespace PzuScrapper.Search;

internal sealed class AuctionSearchService
{
    private const string SearchEndpoint = "/api/auction/auction/search/bidder";

    private readonly HttpClient _http;
    private readonly BidderSearchFiltersRequest? _filters;

    public AuctionSearchService(HttpClient http, BidderSearchFiltersRequest? filters = null)
    {
        _http = http;
        _filters = filters;
    }

    /// <param name="alreadyKnownAuctionNumbers">IDs already saved — pagination stops early when a full page is duplicates.</param>
    public async Task<List<Car>> SearchAllCarsAsync(ISet<string>? alreadyKnownAuctionNumbers = null)
    {
        var allCars = new List<Car>();
        var seenThisRun = new HashSet<string>(StringComparer.Ordinal);
        var currentPage = 0;
        int totalPages;

        do
        {
            var response = await FetchPageAsync(currentPage, allCars.Count);
            if (response is null)
                return allCars;

            var pageRows = response.Result ?? new List<Car>();
            var addedBefore = allCars.Count;

            AddNewCarsFromPage(pageRows, alreadyKnownAuctionNumbers, seenThisRun, allCars);

            if (IsPageFullyKnown(pageRows, alreadyKnownAuctionNumbers))
                return allCars;

            totalPages = response.TotalPages;
            WriteProgressLine(pageRows.Count, newThisPage: allCars.Count - addedBefore, totalCollected: allCars.Count);
            currentPage++;
        }
        while (currentPage < totalPages);

        Console.WriteLine();
        return allCars;
    }

    private async Task<SearchResponse?> FetchPageAsync(int pageNumber, int alreadyCollected)
    {
        var request = BidderSearchRequestFactory.Create(pageNumber, _filters);
        var body = JsonSerializer.Serialize(request, PzuJsonOptions.Write);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.PostAsync(SearchEndpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Scrape", $"Serwer zwrócił kod {(int)response.StatusCode}. Zatrzymuję — zebrano {alreadyCollected}.");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SearchResponse>(json, PzuJsonOptions.Read);
        }
        catch (HttpRequestException ex)
        {
            Log.Error("Scrape", $"Problem sieciowy ({ex.Message}). Zebrano {alreadyCollected}.");
            return null;
        }
        catch (TaskCanceledException)
        {
            Log.Error("Scrape", $"Przekroczono czas oczekiwania. Zebrano {alreadyCollected}.");
            return null;
        }
        catch (JsonException ex)
        {
            Log.Error("Scrape", $"Nie udało się odczytać odpowiedzi ({ex.Message}). Zebrano {alreadyCollected}.");
            return null;
        }
    }

    private static void AddNewCarsFromPage(
        IEnumerable<Car> pageRows,
        ISet<string>? alreadyKnown,
        ISet<string> seenThisRun,
        ICollection<Car> output)
    {
        foreach (var car in pageRows)
        {
            var id = car.AuctionUniqueNumber?.Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (alreadyKnown is not null && alreadyKnown.Contains(id)) continue;
            if (!seenThisRun.Add(id)) continue;

            output.Add(car);
        }
    }

    private static bool IsPageFullyKnown(IReadOnlyCollection<Car> pageRows, ISet<string>? alreadyKnown)
    {
        if (alreadyKnown is null || pageRows.Count == 0) return false;

        var withId = pageRows
            .Where(c => !string.IsNullOrWhiteSpace(c.AuctionUniqueNumber))
            .ToList();

        if (withId.Count == 0) return false;
        if (!withId.All(c => alreadyKnown.Contains(c.AuctionUniqueNumber!.Trim()))) return false;

        Log.Info("Scrape", "Wszystkie pozycje w tej partii są już zapisane — kończę pobieranie listy.");
        return true;
    }

    private static void WriteProgressLine(int pageCount, int newThisPage, int totalCollected)
    {
        var line = $"[Scrape] Pobrano {pageCount} pozycji, nowych: {newThisPage} (razem: {totalCollected}).";
        var width = Console.WindowWidth < 8 ? 120 : Console.WindowWidth;
        Console.Write($"\r{line}{new string(' ', Math.Max(0, width - 1 - line.Length))}");
    }
}
