using System.Text.Json;
using Microsoft.Extensions.Hosting;
using PzuScrapper;
using PzuScrapper.Configuration;
using PzuScrapper.Export;
using PzuScrapper.Models;
using PzuScrapper.Search;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = Host.CreateApplicationBuilder(args);
Log.Info("Konfiguracja", "Start programu.");

AppConfiguration.AddOptionalDevelopmentJson(builder);

// ─── PDF-ONLY MODE (scraping disabled) ───────────────────────────────────
// Wrzuć plik z JSON-em (pojedynczy obiekt CarDetails, tablica, lub JSONL)
// na pulpit jako "pdf-input.json" i uruchom aplikację.
// await RunPdfOnlyModeAsync();
// return;

// ─── Scraping ────────────────────────────────────────────────────────────
try
{
    if (!StartupMenu.Run())
        return;

    var session = AppConfiguration.CreateSiteSession(builder.Configuration);
    var searchFilters = BidderSearchFiltersPrompt.Read();
    await new ScrapeOrchestrator(session, builder.Environment, searchFilters).RunAsync();
}
catch (Exception ex)
{
    Log.Error("App", ex.Message);
}
finally
{
    Console.WriteLine();
    Console.WriteLine("Naciśnij Enter, aby zamknąć okno…");
    Console.Out.Flush();
    Console.ReadLine();
}


#pragma warning disable CS8321 // toggle-on helper, used when scraping lines above are commented out
static async Task RunPdfOnlyModeAsync()
#pragma warning restore CS8321
{
    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    var inputPath = Path.Combine(desktop, "pdf-input.json");

    if (!File.Exists(inputPath))
    {
        Log.Warn("PDF", $"Nie znaleziono pliku wejściowego: {inputPath}");
        return;
    }

    var raw = await File.ReadAllTextAsync(inputPath);
    var cars = ParseCarDetails(raw);

    if (cars.Count == 0)
    {
        Log.Warn("PDF", "Plik nie zawiera żadnych danych pojazdu.");
        return;
    }

    foreach (var car in cars)
    {
        var auctionNo = string.IsNullOrWhiteSpace(car.AuctionUniqueNumber)
            ? car.Id.ToString()
            : car.AuctionUniqueNumber.Trim();
        var outputPath = AppPaths.PdfPathForAuction(auctionNo);

        if (CarDetailsPdf.TryWrite(outputPath, car, photosDirectory: null))
            Log.Info("PDF", $"Zapisano: {outputPath}");
        else
            Log.Error("PDF", $"Nie udało się wygenerować PDF dla {auctionNo}.");
    }

    Log.Info("PDF", "Gotowe.");
}

static List<CarDetails> ParseCarDetails(string raw)
{
    var trimmed = raw.TrimStart();

    if (trimmed.StartsWith('['))
        return JsonSerializer.Deserialize<List<CarDetails>>(trimmed, PzuJsonOptions.Read) ?? [];

    if (trimmed.StartsWith('{'))
    {
        var one = JsonSerializer.Deserialize<CarDetails>(trimmed, PzuJsonOptions.Read);
        return one is null ? [] : [one];
    }

    // JSON Lines fallback
    var list = new List<CarDetails>();
    foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!line.StartsWith('{')) continue;
        var item = JsonSerializer.Deserialize<CarDetails>(line, PzuJsonOptions.Read);
        if (item != null) list.Add(item);
    }
    return list;
}
