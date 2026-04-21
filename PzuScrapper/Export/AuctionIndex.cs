using System.Text.Json;
using System.Text.Json.Serialization;

namespace PzuScrapper.Export;

/// <summary>
/// Persists processed auction IDs for deduplication across runs.
/// Full car data lives in the API; photos in the photos directory.
/// </summary>
internal static class AuctionIndex
{
    private record AuctionEntry([property: JsonPropertyName("id")] string Id);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static HashSet<string> LoadAuctionNumbers(string filePath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(filePath))
            return set;

        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                var entry = ParseLine(line);
                if (entry is not null)
                    set.Add(entry.Id);
            }
        }
        catch
        {
            // Locked or corrupt file — treat as empty.
        }

        return set;
    }

    public static void AppendAuctionNumbers(string filePath, IEnumerable<string> ids)
    {
        var existing = LoadAuctionNumbers(filePath);
        var toAppend = ids
            .Select(id => id.Trim())
            .Where(id => !string.IsNullOrEmpty(id) && existing.Add(id))
            .Select(id => new AuctionEntry(id))
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

        Console.WriteLine($"[Scrape] Dopisano {toAppend.Count} nowych wpisów do {Path.GetFileName(filePath)}.");
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
            return string.IsNullOrEmpty(id) ? null : new AuctionEntry(id);
        }
    }
}
