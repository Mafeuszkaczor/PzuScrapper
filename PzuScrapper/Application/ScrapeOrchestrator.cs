using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Models;
using PzuScrapper.Api;
using PzuScrapper.Auctions;
using PzuScrapper.Auth;
using PzuScrapper.Configuration;
using PzuScrapper.Export;
using PzuScrapper.Models;
using PzuScrapper.Scraping;
using PzuScrapper.Session;

namespace PzuScrapper.Application;

/// <summary>
/// Main flow: browser → login → API → CSV → photos from auction detail pages.
/// </summary>
public sealed class ScrapeOrchestrator
{
    private readonly SiteSession _siteSession;
    private readonly PzuSessionPersistence _sessionPersistence;
    private readonly BidderSearchFilters? _searchFilters;

    public ScrapeOrchestrator(
        SiteSession siteSession,
        IHostEnvironment hostEnvironment,
        BidderSearchFilters? searchFilters = null)
    {
        _siteSession = siteSession;
        _sessionPersistence = new PzuSessionPersistence(hostEnvironment.EnvironmentName);
        _searchFilters = searchFilters;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });

        Console.WriteLine("[Scrape] Otwieram przeglądarkę…");
        var context = await browser.NewContextAsync(_sessionPersistence.BuildNewContextOptions());
        var page = await context.NewPageAsync();

        var authFlow = new PzuAuthFlow(page, _siteSession);
        if (!await authFlow.EstablishSessionAsync())
            return;

        await _sessionPersistence.SaveAsync(context);

        var token = await SessionTokenReader.ReadAsync(page);
        using var http = await PzuHttpClientFactory.CreateAsync(token, context);

        var csvPath = AutaCsvPaths.DesktopAutaCsv;
        var alreadyInFile = CarCsvFile.LoadExistingAuctionNumbers(csvPath);
        Console.WriteLine(
            $"[Scrape] Plik {Path.GetFileName(csvPath)} – już zapisane oferty: {alreadyInFile.Count}.");

        var newCars = await FetchNewCarsFromApiAsync(http, alreadyInFile);
        Console.WriteLine($"[Scrape] Do przetworzenia: {newCars.Count} nowych ofert.");

        CarCsvFile.AppendNewCars(csvPath, newCars);

        var carsForPhotos = ResolveCarsForPhotoPass(newCars, csvPath);
        if (newCars.Count == 0 && carsForPhotos.Count > 0)
            Console.WriteLine(
                $"[Scrape] Brak nowych pozycji z listy – pobieram zdjęcia z zapisanego pliku ({carsForPhotos.Count} ofert).");

        await DownloadPhotosAsync(page, http, carsForPhotos, cancellationToken);

        Console.WriteLine("[Scrape] Gotowe.");
    }

    private async Task<List<Car>> FetchNewCarsFromApiAsync(HttpClient http, HashSet<string> alreadyInFile)
    {
        Console.WriteLine("[Scrape] Pobieram listę ofert (pomijam już zapisane)…");
        return await new AuctionSearchService(http, _searchFilters).SearchAllCarsAsync(alreadyInFile);
    }

    private static List<Car> ResolveCarsForPhotoPass(List<Car> newCars, string csvPath) =>
        newCars.Count > 0 ? newCars : CarCsvFile.LoadCarsFromCsv(csvPath);

    private static async Task DownloadPhotosAsync(
        IPage page,
        HttpClient http,
        IReadOnlyList<Car> carsForPhotos,
        CancellationToken cancellationToken)
    {
        var photoScraper = new CarPhotoScraper();
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
            await photoScraper.GetPhotosAsync(page, car, http);
        }
    }
}
