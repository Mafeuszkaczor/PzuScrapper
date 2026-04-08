using Microsoft.Playwright;
using PlaywrightTests;
using PzuScrapper.Api;
using PzuScrapper.Auctions;
using PzuScrapper.Auth;
using PzuScrapper.Export;
using PzuScrapper.Models;
using PzuScrapper.Session;

namespace PzuScrapper;

public class Scrapper
{
    private readonly SiteSession _siteSession;
    private readonly PzuSessionPersistence _sessionPersistence = new();

    public Scrapper(SiteSession siteSession)
    {
        _siteSession = siteSession;
    }

    public async Task Scrape()
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });

        Console.WriteLine("[Scrapper] Uruchamiam przeglądarkę...");
        var context = await browser.NewContextAsync();
        await _sessionPersistence.RestoreAsync(context);

        var page = await context.NewPageAsync();
        var authFlow = new PzuAuthFlow(page, _siteSession);
        if (!await authFlow.EstablishSessionAsync())
            return;

        await _sessionPersistence.SaveAsync(context, page);

        var token = await SessionTokenReader.ReadAsync(page);
        using var http = await PzuHttpClientFactory.CreateAsync(token, context);

        Console.WriteLine("[Scrapper] Pobieram listę aukcji...");
        var cars = await new AuctionSearchService(http).SearchAllCarsAsync();
        Console.WriteLine($"[Scrapper] Znaleziono {cars.Count} samochodów łącznie.");

        CsvExporter.Save(cars, "auta.csv");

        var photoScraper = new CarPhotoScraper();
        for (var i = 0; i < cars.Count; i++)
        {
            var car = cars[i];
            var url = $"https://ppo.pzu.pl/bidder/auction/details/vs/{car.auctionUniqueNumber}/vehicle-sale";
            Console.WriteLine($"[Scrapper] ({i + 1}/{cars.Count}) {car.manufacturer} {car.model} [{car.auctionUniqueNumber}]");

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await photoScraper.GetPhotosAsync(page, car, http);
        }

        Console.WriteLine("[Scrapper] Gotowe.");
    }
}
