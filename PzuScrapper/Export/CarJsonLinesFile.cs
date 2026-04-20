using System.Text.Json;
using Models;

namespace PzuScrapper.Export;

internal static class CarJsonLinesFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static HashSet<string> LoadExistingAuctionNumbers(string filePath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(filePath))
            return set;

        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var car = JsonSerializer.Deserialize<Car>(line, JsonOptions);
                    if (!string.IsNullOrWhiteSpace(car?.auctionUniqueNumber))
                        set.Add(car.auctionUniqueNumber);
                }
                catch
                {
                    // Skip malformed line and continue with best effort.
                }
            }
        }
        catch
        {
            // Corrupt or locked file - treat as empty.
        }

        return set;
    }

    public static List<Car> LoadCarsFromJsonLines(string filePath)
    {
        var list = new List<Car>();
        if (!File.Exists(filePath))
            return list;

        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var car = JsonSerializer.Deserialize<Car>(line, JsonOptions);
                    if (!string.IsNullOrWhiteSpace(car?.auctionUniqueNumber))
                        list.Add(car);
                }
                catch
                {
                    // Skip malformed line and continue with best effort.
                }
            }
        }
        catch
        {
            return list;
        }

        return list;
    }

    public static void AppendNewCars(string filePath, IReadOnlyList<Car> newCars)
    {
        if (newCars.Count == 0)
        {
            Console.WriteLine("Brak nowych ofert do dopisania do pliku.");
            return;
        }

        var parentDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(parentDir))
            Directory.CreateDirectory(parentDir);

        var existingAuctionNumbers = LoadExistingAuctionNumbers(filePath);
        var linesToAppend = new List<string>(newCars.Count);

        foreach (var car in newCars)
        {
            if (string.IsNullOrWhiteSpace(car.auctionUniqueNumber))
                continue;
            if (!existingAuctionNumbers.Add(car.auctionUniqueNumber))
                continue;

            linesToAppend.Add(JsonSerializer.Serialize(car, JsonOptions));
        }

        if (linesToAppend.Count == 0)
        {
            Console.WriteLine("Brak nowych ofert do dopisania po deduplikacji.");
            return;
        }

        using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream))
        {
            foreach (var line in linesToAppend)
                writer.WriteLine(line);
        }

        Console.WriteLine(
            $"Dane JSONL dopisane do {Path.GetFileName(filePath)} ({linesToAppend.Count} nowych rekordow).");
    }

    // Temporary compatibility for still-migrating callers.
    public static List<Car> LoadCarsFromCsv(string filePath) => LoadCarsFromJsonLines(filePath);
}
