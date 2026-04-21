using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Models;
using PzuScrapper.Auth;
using PzuScrapper.Configuration;
using PzuScrapper.Export;
using PzuScrapper.Http;
using PzuScrapper.Models;
using PzuScrapper.Scraping;
using PzuScrapper.Search;

namespace PzuScrapper;

/// <summary>
/// Main flow: browser → login → API → auction index → photos → PDF.
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
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false });

        Console.WriteLine("[Scrape] Otwieram przeglądarkę…");
        var context = await browser.NewContextAsync(_sessionPersistence.BuildNewContextOptions());
        var page = await context.NewPageAsync();

        using var baggageCapture = new BaggageHeaderCapture(page);

        if (!await new PzuAuthFlow(page, _siteSession).EstablishSessionAsync())
            return;

        await _sessionPersistence.SaveAsync(context);

        var userUuid = await baggageCapture.WaitAsync(TimeSpan.FromSeconds(5));
        if (userUuid is null)
            Console.WriteLine("[Scrape] Ostrzeżenie: nie udało się odczytać baggage-user-uuid — API może zwracać 401.");
        else
            Console.WriteLine("[Scrape] Nagłówek baggage-user-uuid odczytany.");

        using var http = await PzuHttpClient.CreateAsync(page, context, userUuid);

        var newCars = await FetchAndPersistNewCarsAsync(http);
        await GetCarsAsync(page, http, newCars, cancellationToken);

        Console.WriteLine("[Scrape] Gotowe.");
    }

    private async Task<List<Car>> FetchAndPersistNewCarsAsync(HttpClient http)
    {
        var jsonlPath = AppPaths.CarsJsonLinesPath;
        var knownIds = AuctionIndex.LoadAuctionNumbers(jsonlPath);
        Console.WriteLine($"[Scrape] Plik {Path.GetFileName(jsonlPath)} — zapisanych ID: {knownIds.Count}.");

        Console.WriteLine("[Scrape] Pobieram listę ofert (pomijam już zapisane)…");
        var newCars = await new AuctionSearchService(http, _searchFilters).SearchAllCarsAsync(knownIds);
        Console.WriteLine($"[Scrape] Do przetworzenia: {newCars.Count} nowych ofert.");

        var newIds = newCars
            .Select(c => c.auctionUniqueNumber)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>();
        AuctionIndex.AppendAuctionNumbers(jsonlPath, newIds);

        return newCars;
    }

    private static async Task GetCarsAsync(
        IPage page,
        HttpClient client,
        IReadOnlyList<Car> newCars,
        CancellationToken cancellationToken)
    {
        var photoScraper = new CarPhotoScraper();
        var carDataService = new CarDataQueryService(client);

        // Phase 1 — new cars: navigate, download photos, generate PDF.
        for (var i = 0; i < newCars.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var car = newCars[i];
            if (string.IsNullOrWhiteSpace(car.auctionUniqueNumber))
                continue;

            var auctionNo = car.auctionUniqueNumber.Trim();
            var pdfPath = AppPaths.PdfPathForAuction(auctionNo);

            if (File.Exists(pdfPath))
            {
                Console.WriteLine($"[Scrape] ({i + 1}/{newCars.Count}) {car.manufacturer} {car.model} — PDF gotowy, pomijam.");
                continue;
            }

            var photoDir = CarPhotoScraper.ResolvePhotoDirectory(car);
            if (!Directory.Exists(photoDir) || !Directory.EnumerateFiles(photoDir).Any())
            {
                Console.WriteLine($"[Scrape] Zdjęcia ({i + 1}/{newCars.Count}) {car.manufacturer} {car.model}");
                await page.GotoAsync(
                    PzuPortalUrls.VehicleSaleDetails(auctionNo),
                    new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                await photoScraper.GetPhotosAsync(page, car, client);
            }

            await TryGeneratePdfAsync(carDataService, auctionNo, photoDir, pdfPath);
        }

        // Phase 2 — recovery: generate PDFs for photos left from interrupted runs.
        await RecoverOrphanedPhotosAsync(carDataService, newCars, cancellationToken);
    }

    private static async Task RecoverOrphanedPhotosAsync(
        CarDataQueryService carDataService,
        IReadOnlyList<Car> newCars,
        CancellationToken cancellationToken)
    {
        var photosRoot = AppPaths.PhotosDirectory;
        if (!Directory.Exists(photosRoot))
            return;

        var handledDirs = newCars
            .Select(c => CarPhotoScraper.ResolvePhotoDirectory(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphaned = Directory
            .EnumerateDirectories(photosRoot)
            .Where(dir => !handledDirs.Contains(dir) && Directory.EnumerateFiles(dir).Any())
            .ToList();

        if (orphaned.Count == 0)
            return;

        Console.WriteLine($"[Scrape] Znaleziono {orphaned.Count} folderów ze zdjęciami bez PDF — generuję…");
        var recovered = 0;
        for (var i = 0; i < orphaned.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var photoDir = orphaned[i];
            var id = Path.GetFileName(photoDir);
            var pdfPath = AppPaths.PdfPathForAuction(id);

            if (File.Exists(pdfPath))
            {
                CarDetailsPdf.TryDeletePhotoDirectory(photoDir);
                continue;
            }

            Console.WriteLine($"[Scrape] Odzysk: {id}");
            if (await TryGeneratePdfAsync(carDataService, id, photoDir, pdfPath))
                recovered++;
        }

        if (recovered > 0)
            Console.WriteLine($"[Scrape] Odzyskano {recovered} PDF z poprzednich uruchomień.");
    }

    private static async Task<bool> TryGeneratePdfAsync(
        CarDataQueryService carDataService,
        string auctionNo,
        string photoDir,
        string pdfPath)
    {
        var carDetails = await carDataService.GetCarDataAsync(auctionNo);
        if (carDetails is null)
        {
            Console.WriteLine($"[PDF] Brak danych API dla oferty {auctionNo} — pomijam PDF.");
            return false;
        }

        if (!CarDetailsPdf.TryWrite(pdfPath, carDetails, photoDir))
            return false;

        Console.WriteLine($"[PDF] Zapisano: {pdfPath}");
        CarDetailsPdf.TryDeletePhotoDirectory(photoDir);
        return true;
    }
}
