using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Models;
using PzuScrapper.Api;
using PzuScrapper.Auctions;
using PzuScrapper.Auth;
using PzuScrapper.Configuration;
using PzuScrapper.Export;
using PzuScrapper.Models;
using PzuScrapper.Models.Request;
using PzuScrapper.Scraping;
using PzuScrapper.Session;

namespace PzuScrapper.Application;

/// <summary>
/// Main flow: browser -> login -> API -> JSONL -> photos from auction detail pages.
/// </summary>
public sealed class ScrapeOrchestrator
{
    private readonly SiteSession _siteSession;
    private readonly PzuSessionPersistence _sessionPersistence;
    private readonly BidderSearchFiltersRequest? _searchFilters;

    public ScrapeOrchestrator(
        SiteSession siteSession,
        IHostEnvironment hostEnvironment,
        BidderSearchFiltersRequest? searchFilters = null)
    {
        _siteSession = siteSession;
        _sessionPersistence = new PzuSessionPersistence(hostEnvironment.EnvironmentName);
        _searchFilters = searchFilters;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        Console.WriteLine("[Scrape] Otwieram przeglądarkę…");
        var context = await browser.NewContextAsync(_sessionPersistence.BuildNewContextOptions());
        var page = await context.NewPageAsync();

        if (!await EstablishSessionAsync(page))
            return;

        await _sessionPersistence.SaveAsync(context);
        using var http = await CreateApiClientAsync(page, context);
        var jsonlPath = AutaCsvPaths.CarsJsonLinesPath;
        var (carsForPhotos, newAuctionNumbers) = await FetchPersistAndResolveCarsForPhotosAsync(http, jsonlPath);
        await GetCarsAsync(page, http, carsForPhotos, newAuctionNumbers, cancellationToken);
        Console.WriteLine("[Scrape] Gotowe.");
    }

    private async Task<bool> EstablishSessionAsync(IPage page)
    {
        var authFlow = new PzuAuthFlow(page, _siteSession);
        return await authFlow.EstablishSessionAsync();
    }

    private static async Task<HttpClient> CreateApiClientAsync(IPage page, IBrowserContext context)
    {
        var token = await SessionTokenReader.ReadAsync(page);
        return await PzuHttpClientFactory.CreateAsync(token, context);
    }

    private async Task<(List<Car> CarsForPhotos, HashSet<string> NewAuctionNumbers)> FetchPersistAndResolveCarsForPhotosAsync(
        HttpClient http,
        string jsonlPath)
    {
        var alreadyInFile = LoadAlreadySavedAuctionNumbers(jsonlPath);
        var newCars = await FetchNewCarsFromApiAsync(http, alreadyInFile);
        Console.WriteLine($"[Scrape] Do przetworzenia: {newCars.Count} nowych ofert.");

        CarJsonLinesFile.AppendNewCars(jsonlPath, newCars);

        var newAuctionNumbers = newCars
            .Select(c => c.auctionUniqueNumber)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.Ordinal);

        var carsForPhotos = ResolveCarsForPhotoPass(newCars, jsonlPath);
        if (newCars.Count == 0 && carsForPhotos.Count > 0)
            Console.WriteLine(
                $"[Scrape] Brak nowych pozycji z listy - pobieram zdjecia z zapisanego pliku ({carsForPhotos.Count} ofert).");

        return (carsForPhotos, newAuctionNumbers);
    }

    private static HashSet<string> LoadAlreadySavedAuctionNumbers(string jsonlPath)
    {
        var alreadyInFile = CarJsonLinesFile.LoadExistingAuctionNumbers(jsonlPath);
        Console.WriteLine(
            $"[Scrape] Plik {Path.GetFileName(jsonlPath)} - zapisanych ofert: {alreadyInFile.Count}.");
        return alreadyInFile;
    }

    private async Task<List<Car>> FetchNewCarsFromApiAsync(HttpClient http, HashSet<string> alreadyInFile)
    {
        Console.WriteLine("[Scrape] Pobieram listę ofert (pomijam już zapisane)…");
        return await new AuctionSearchService(http, _searchFilters).SearchAllCarsAsync(alreadyInFile);
    }

    private static List<Car> ResolveCarsForPhotoPass(List<Car> newCars, string jsonlPath) =>
        newCars.Count > 0 ? newCars : CarJsonLinesFile.LoadCarsFromJsonLines(jsonlPath);

    private static async Task GetCarsAsync(
        IPage page,
        HttpClient client,
        IReadOnlyList<Car> carsForPhotos,
        IReadOnlySet<string> newAuctionNumbers,
        CancellationToken cancellationToken)
    {
        var photoScraper = new CarPhotoScraper();
        var carDataQueryService = new CarDataQueryService(client);
        for (var i = 0; i < carsForPhotos.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var car = carsForPhotos[i];
            if (string.IsNullOrWhiteSpace(car.auctionUniqueNumber))
                continue;

            var url = PzuPortalUrls.VehicleSaleDetails(car.auctionUniqueNumber);
            Console.WriteLine(
                $"[Scrape] Zdjęcia ({i + 1}/{carsForPhotos.Count}) {car.manufacturer} {car.model}");

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await photoScraper.GetPhotosAsync(page, car, client);

            var auctionNo = car.auctionUniqueNumber.Trim();
            if (!newAuctionNumbers.Contains(auctionNo))
                continue;

            var carDetails = await carDataQueryService.GetCarDataAsync(car.auctionUniqueNumber);
            if (carDetails is null)
            {
                Console.WriteLine($"[PDF] Brak danych API dla oferty {auctionNo} — pomijam PDF.");
                continue;
            }

            var photoDir = CarPhotoScraper.ResolvePhotoDirectory(car);
            var pdfPath = ImportPaths.PdfPathForAuction(auctionNo);
            if (CarDetailsPdfWriter.TryWrite(pdfPath, carDetails, photoDir))
            {
                Console.WriteLine($"[PDF] Zapisano: {pdfPath}");
                CarDetailsPdfWriter.TryDeletePhotoDirectory(photoDir);
            }
        }
    }
}
