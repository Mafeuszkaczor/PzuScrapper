using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PzuScrapper.Export;

/// <summary>Entry persisted in cars.jsonl: which auction was scraped and when.</summary>
internal sealed record AuctionEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("scrapedOn")] string? ScrapedOn)
{
    public DateOnly ScrapedOnDate =>
        DateOnly.TryParseExact(ScrapedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : DateOnly.MinValue; // legacy entries without date → always treated as "not today"
}

/// <summary>
/// Persists processed auction IDs (+ scrape date) for deduplication across runs.
/// Full car data lives in the API; photos in the photos directory.
/// </summary>
internal static class AuctionIndex
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static IReadOnlyList<AuctionEntry> LoadEntries(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var list = new List<AuctionEntry>();
        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                var entry = ParseLine(line);
                if (entry is not null)
                    list.Add(entry);
            }
        }
        catch
        {
            // Locked or corrupt file — treat as empty.
        }
        return list;
    }

    public static void AppendEntries(string filePath, IEnumerable<AuctionEntry> newEntries)
    {
        var existingIds = LoadEntries(filePath).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var toAppend = newEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Id) && existingIds.Add(e.Id.Trim()))
            .Select(e => e with { Id = e.Id.Trim() })
            .ToList();

        if (toAppend.Count == 0)
            return;

        var parentDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(parentDir))
            Directory.CreateDirectory(parentDir);

        using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        foreach (var entry in toAppend)
            writer.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));

        Log.Info("Scrape", $"Dopisano {toAppend.Count} nowych wpisów do {Path.GetFileName(filePath)}.");
    }

    public static void Clear(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Info("Ustawienia", $"Plik {Path.GetFileName(filePath)} nie istnieje — nic do wyczyszczenia.");
            return;
        }
        File.Delete(filePath);
        Log.Info("Ustawienia", $"Usunięto plik {Path.GetFileName(filePath)}.");
    }

    private static AuctionEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        try
        {
            return JsonSerializer.Deserialize<AuctionEntry>(line, JsonOptions);
        }
        catch
        {
            // Fallback: plain-text ID from old format
            var id = line.Trim();
            return string.IsNullOrEmpty(id) ? null : new AuctionEntry(id, ScrapedOn: null);
        }
    }
}
