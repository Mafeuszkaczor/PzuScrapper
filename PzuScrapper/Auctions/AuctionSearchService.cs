using System.Text;
using System.Text.Json;
using Models;

namespace PzuScrapper.Auctions;

internal sealed class AuctionSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public AuctionSearchService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Car>> SearchAllCarsAsync()
    {
        var allCars = new List<Car>();
        var currentPage = 0;
        int totalPages;

        do
        {
            var request = BidderSearchRequestFactory.Create(currentPage);
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("/api/auction/auction/search/bidder", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonSerializer.Deserialize<SearchResponse>(responseJson, JsonOptions);

            if (searchResponse?.result != null)
                allCars.AddRange(searchResponse.result);

            totalPages = searchResponse?.totalPages ?? 1;
            Console.WriteLine($"Strona {currentPage + 1}/{totalPages} – pobrano {searchResponse?.result?.Count ?? 0} aukcji");

            currentPage++;
        }
        while (currentPage < totalPages);

        return allCars;
    }
}
