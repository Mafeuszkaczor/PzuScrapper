using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
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
    // Nadpisuje typowe markery, po których anti-bot (Secfense) wykrywa headless/Playwright.
    private const string AntiDetectInitScript = """
        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
        Object.defineProperty(navigator, 'languages', { get: () => ['pl-PL', 'pl', 'en-US', 'en'] });
        Object.defineProperty(navigator, 'plugins', {
            get: () => [
                { name: 'PDF Viewer' },
                { name: 'Chrome PDF Viewer' },
                { name: 'Chromium PDF Viewer' },
                { name: 'Microsoft Edge PDF Viewer' },
                { name: 'WebKit built-in PDF' },
            ],
        });
        window.chrome = window.chrome || { runtime: {} };
        const originalQuery = window.navigator.permissions && window.navigator.permissions.query;
        if (originalQuery) {
            window.navigator.permissions.query = (parameters) =>
                parameters.name === 'notifications'
                    ? Promise.resolve({ state: Notification.permission })
                    : originalQuery(parameters);
        }
        """;

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

        var debug = AuthDiagnostics.IsEnabled;
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !debug,
            SlowMo = debug ? 100 : 0,
            Args = new[]
            {
                "--lang=pl-PL",
                "--disable-blink-features=AutomationControlled",
            },
        });

        Log.Info("Scrape", debug ? "Otwieram przeglądarkę (DEBUG – widoczne okno)…" : "Otwieram przeglądarkę…");

        // Realistyczne UA/locale/viewport — headless domyślnie ma "HeadlessChrome" w UA i 800x600.
        var contextOptions = _sessionPersistence.BuildNewContextOptions();
        contextOptions.UserAgent ??= "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
        contextOptions.Locale ??= "pl-PL";
        contextOptions.TimezoneId ??= "Europe/Warsaw";
        contextOptions.ViewportSize ??= new ViewportSize { Width = 1366, Height = 768 };
        var extraHeaders = contextOptions.ExtraHTTPHeaders?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        extraHeaders["Accept-Language"] = "pl-PL,pl;q=0.9,en-US;q=0.8,en;q=0.7";
        contextOptions.ExtraHTTPHeaders = extraHeaders;

        var context = await browser.NewContextAsync(contextOptions);
        await context.AddInitScriptAsync(AntiDetectInitScript);

        var page = await context.NewPageAsync();

        using var baggageCapture = new BaggageHeaderCapture(page);
        if (!await new PzuAuthFlow(page, _siteSession).EstablishSessionAsync())
            return;

        await _sessionPersistence.SaveAsync(context);

        var userUuid = await baggageCapture.WaitAsync(TimeSpan.FromSeconds(5));
        if (userUuid is null)
            Log.Warn("Scrape", "nie udało się odczytać baggage-user-uuid — API może zwracać 401.");
        else
            Log.Info("Scrape", "Nagłówek baggage-user-uuid odczytany.");

        using var http = await PzuHttpClient.CreateAsync(page, context, userUuid);

        var newCars = await FetchAndPersistNewCarsAsync(http);

        await ProcessNewCarsAsync(page, context, http, newCars, cancellationToken);
        await PzuHttpClient.RefreshAsync(http, page, context);
        await RecoverOrphanedPhotosAsync(http, newCars, cancellationToken);

        Log.Info("Scrape", "Gotowe.");
    }

    // ─── Phases ────────────────────────────────────────────────────────────

    private async Task<List<Car>> FetchAndPersistNewCarsAsync(HttpClient http)
    {
        var jsonlPath = AppPaths.CarsJsonLinesPath;
        var entries = AuctionIndex.LoadEntries(jsonlPath);
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Entries from today whose PDF was deleted → re-scrape them.
        // Older entries are always skipped (past days don't need regeneration).
        var knownIds = entries
            .Where(e => e.ScrapedOnDate != today || File.Exists(AppPaths.PdfPathForAuction(e.Id)))
            .Select(e => e.Id)
            .ToHashSet(StringComparer.Ordinal);

        var todayMissingPdf = entries.Count(e => e.ScrapedOnDate == today && !File.Exists(AppPaths.PdfPathForAuction(e.Id)));
        Log.Info("Scrape", $"Plik {Path.GetFileName(jsonlPath)} — zapisanych ID: {entries.Count} (do ponownego przetworzenia z dziś: {todayMissingPdf}).");

        Log.Info("Scrape", "Pobieram listę ofert (pomijam już zapisane)…");
        var newCars = await new AuctionSearchService(http, _searchFilters).SearchAllCarsAsync(knownIds);
        Log.Info("Scrape", $"Do przetworzenia: {newCars.Count} nowych ofert.");

        var newEntries = newCars
            .Select(c => c.AuctionUniqueNumber)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(id => new AuctionEntry(id!.Trim(), today.ToString("yyyy-MM-dd")));
        AuctionIndex.AppendEntries(jsonlPath, newEntries);

        return newCars;
    }

    /// <summary>
    /// Phase 1 — new cars. API call first (while cookies are fresh), then photos.
    /// PPO rotates MRHSession server-side during each /vehicle-sale navigation, so
    /// the HttpClient's cookie header goes stale every time photos are downloaded.
    /// We refresh the HttpClient from the live browser context before each API call.
    /// </summary>
    private static async Task ProcessNewCarsAsync(
        IPage page,
        IBrowserContext context,
        HttpClient client,
        IReadOnlyList<Car> newCars,
        CancellationToken cancellationToken)
    {
        var photoScraper = new CarPhotoScraper();
        var carDataService = new CarDataQueryService(client);

        for (var i = 0; i < newCars.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var car = newCars[i];
            if (string.IsNullOrWhiteSpace(car.AuctionUniqueNumber))
                continue;

            var auctionNo = car.AuctionUniqueNumber.Trim();
            var pdfPath = AppPaths.PdfPathForAuction(auctionNo);

            if (File.Exists(pdfPath))
            {
                Log.Info("Scrape", $"({i + 1}/{newCars.Count}) {car.Manufacturer} {car.Model} — PDF gotowy, pomijam.");
                continue;
            }

            Log.Info("Scrape", $"({i + 1}/{newCars.Count}) {car.Manufacturer} {car.Model} — pobieram dane z API…");
            await PzuHttpClient.RefreshAsync(client, page, context);
            var carDetails = await carDataService.GetCarDataAsync(auctionNo);
            if (carDetails is null)
            {
                Log.Warn("Scrape", $"Brak danych API dla oferty {auctionNo} — pomijam cały wpis.");
                continue;
            }

            var photoDir = CarPhotoScraper.ResolvePhotoDirectory(car);
            if (!HasDownloadedPhotos(photoDir))
            {
                Log.Info("Scrape", $"Zdjęcia ({i + 1}/{newCars.Count}) {car.Manufacturer} {car.Model}");
                await page.GotoAsync(
                    PzuPortalUrls.VehicleSaleDetails(auctionNo),
                    new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                await photoScraper.GetPhotosAsync(page, car, client);
            }

            await WritePdfAsync(carDetails, auctionNo, photoDir, pdfPath);
        }
    }

    /// <summary>Phase 2 — recovery: generate PDFs for photo folders left from interrupted runs.</summary>
    private static async Task RecoverOrphanedPhotosAsync(
        HttpClient client,
        IReadOnlyList<Car> newCars,
        CancellationToken cancellationToken)
    {
        var photosRoot = AppPaths.PhotosDirectory;
        if (!Directory.Exists(photosRoot))
            return;

        var handledDirs = newCars
            .Select(CarPhotoScraper.ResolvePhotoDirectory)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphaned = Directory
            .EnumerateDirectories(photosRoot)
            .Where(dir => !handledDirs.Contains(dir) && HasDownloadedPhotos(dir))
            .ToList();

        if (orphaned.Count == 0)
            return;

        Log.Info("Scrape", $"Znaleziono {orphaned.Count} folderów ze zdjęciami bez PDF — generuję…");
        var carDataService = new CarDataQueryService(client);
        var recovered = 0;

        foreach (var photoDir in orphaned)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var id = Path.GetFileName(photoDir);
            var pdfPath = AppPaths.PdfPathForAuction(id);

            if (File.Exists(pdfPath))
            {
                CarDetailsPdf.TryDeletePhotoDirectory(photoDir);
                continue;
            }

            Log.Info("Scrape", $"Odzysk: {id}");
            if (await TryGeneratePdfAsync(carDataService, id, photoDir, pdfPath))
                recovered++;
        }

        if (recovered > 0)
            Log.Info("Scrape", $"Odzyskano {recovered} PDF z poprzednich uruchomień.");
    }

    // ─── Shared helpers ────────────────────────────────────────────────────

    private static bool HasDownloadedPhotos(string directory) =>
        Directory.Exists(directory) && Directory.EnumerateFiles(directory).Any();

    private static async Task<bool> TryGeneratePdfAsync(
        CarDataQueryService carDataService,
        string auctionNo,
        string photoDir,
        string pdfPath)
    {
        var carDetails = await carDataService.GetCarDataAsync(auctionNo);
        if (carDetails is null)
        {
            Log.Warn("PDF", $"Brak danych API dla oferty {auctionNo} — pomijam PDF.");
            return false;
        }

        return await WritePdfAsync(carDetails, auctionNo, photoDir, pdfPath);
    }

    private static async Task<bool> WritePdfAsync(CarDetails carDetails, string auctionNo, string photoDir, string pdfPath)
    {
        using var cts = new CancellationTokenSource();
        var spinner = RunPdfSpinnerAsync(cts.Token);

        var success = await Task.Run(() => CarDetailsPdf.TryWrite(pdfPath, carDetails, photoDir));

        cts.Cancel();
        await spinner;
        ClearCurrentConsoleLine();

        if (!success)
        {
            Log.Warn("PDF", $"Nie udało się zapisać PDF dla {auctionNo}.");
            return false;
        }

        Log.Info("PDF", $"Zapisano: {pdfPath}");
        CarDetailsPdf.TryDeletePhotoDirectory(photoDir);
        return true;
    }

    private static async Task RunPdfSpinnerAsync(CancellationToken token)
    {
        const string prefix = "[PDF] Zapisywanie raportu";
        var dotCount = 1;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var dots = new string('.', dotCount).PadRight(5);
                Console.Write($"\r{prefix} {dots}");
                await Task.Delay(300, token);
                dotCount = dotCount == 5 ? 1 : dotCount + 1;
            }
        }
        catch (TaskCanceledException) { }
    }

    private static void ClearCurrentConsoleLine()
    {
        var width = Console.WindowWidth < 8 ? 80 : Console.WindowWidth - 1;
        Console.Write('\r' + new string(' ', width) + '\r');
    }
}
