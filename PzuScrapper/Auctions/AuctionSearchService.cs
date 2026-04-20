using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Models;
using PzuScrapper.Models.Request;
using PzuScrapper.Models.Response;

namespace PzuScrapper.Auctions;

internal sealed class AuctionSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(), new UtcIsoDateTimeConverter() },
    };

    private readonly HttpClient _http;
    private readonly BidderSearchFiltersRequest? _filters;

    public AuctionSearchService(HttpClient http, BidderSearchFiltersRequest? filters = null)
    {
        _http = http;
        _filters = filters;
    }

    /// <param name="alreadyHaveAuctionNumbers">Auction IDs already saved (skipped); pagination stops early if a full page is duplicates.</param>
    public async Task<List<Car>> SearchAllCarsAsync(ISet<string>? alreadyHaveAuctionNumbers = null)
    {
        var allCars = new List<Car>();
        var seenThisRun = new HashSet<string>(StringComparer.Ordinal);
        var currentPage = 0;
        int totalPages;

        do
        {
            var searchResponse = await FetchPageAsync(currentPage, allCars.Count);
            if (searchResponse is null)
                return allCars;

            var pageRows = searchResponse?.result;
            var pageCount = pageRows?.Count ?? 0;
            var addedBeforePage = allCars.Count;

            if (pageRows is { Count: > 0 })
            {
                AddNewCarsFromPage(pageRows, alreadyHaveAuctionNumbers, seenThisRun, allCars);
                if (IsPageFullyKnown(pageRows, alreadyHaveAuctionNumbers))
                    return allCars;
            }

            totalPages = searchResponse?.totalPages ?? 1;
            var newThisPage = allCars.Count - addedBeforePage;
            WriteListFetchProgressLine(pageCount, newThisPage, allCars.Count);

            currentPage++;
        }
        while (currentPage < totalPages);

        Console.WriteLine();
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

    private async Task<SearchResponse?> FetchPageAsync(int currentPage, int alreadyCollected)
    {
        var request = BidderSearchRequestFactory.Create(currentPage, _filters);
        var json = JsonSerializer.Serialize(request, RequestJsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.PostAsync("/api/auction/auction/search/bidder", content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"\n[Scrape] Serwer zwrócił błąd (kod {(int)response.StatusCode}). "
                    + $"Zatrzymuję pobieranie — zebrano już {alreadyCollected} pozycji.");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SearchResponse>(responseJson, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(
                $"\n[Scrape] Problem z połączeniem sieciowym. Zatrzymuję pobieranie — zebrano {alreadyCollected} pozycji. ({ex.Message})");
            return null;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine(
                $"\n[Scrape] Przekroczono czas oczekiwania na odpowiedź. Zatrzymuję pobieranie — zebrano {alreadyCollected} pozycji.");
            return null;
        }
        catch (JsonException)
        {
            Console.WriteLine(
                $"\n[Scrape] Nie udało się odczytać odpowiedzi serwera. Zatrzymuję pobieranie — zebrano {alreadyCollected} pozycji.");
            return null;
        }
    }

    private static void AddNewCarsFromPage(
        IEnumerable<global::Models.Car> pageRows,
        ISet<string>? alreadyHaveAuctionNumbers,
        ISet<string> seenThisRun,
        ICollection<Car> output)
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
            output.Add(car);
        }
    }

    private static bool IsPageFullyKnown(IEnumerable<global::Models.Car> pageRows, ISet<string>? alreadyHaveAuctionNumbers)
    {
        if (alreadyHaveAuctionNumbers is null)
            return false;

        var withId = pageRows.Where(c => !string.IsNullOrWhiteSpace(c.auctionUniqueNumber)).ToList();
        if (withId.Count == 0)
            return false;

        var allKnown = withId.All(c => alreadyHaveAuctionNumbers.Contains(c.auctionUniqueNumber!.Trim()));
        if (!allKnown)
            return false;

        Console.WriteLine(
            "\n[Scrape] Wszystkie pozycje w tej partii wyników są już zapisane – kończę pobieranie listy.");
        return true;
    }
}
