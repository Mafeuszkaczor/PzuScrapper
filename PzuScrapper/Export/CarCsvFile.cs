using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Models;

namespace PzuScrapper.Export;

internal static class CarCsvFile
{
    private const char Delimiter = ';';

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly Lazy<PropertyInfo[]> CarPropertiesOrdered = new(() =>
        typeof(Car).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.MetadataToken)
            .ToArray());

    public static HashSet<string> LoadExistingAuctionNumbers(string filePath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(filePath))
            return set;

        try
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            var headerLine = reader.ReadLine();
            if (headerLine is null)
                return set;

            var headers = ParseCsvLine(headerLine);
            var idx = headers.FindIndex(h =>
                string.Equals(h.Trim(), nameof(Car.auctionUniqueNumber), StringComparison.Ordinal));
            if (idx < 0)
                return set;

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var fields = ParseCsvLine(line);
                if (idx >= fields.Count)
                    continue;
                var id = fields[idx].Trim();
                if (id.Length > 0)
                    set.Add(id);
            }
        }
        catch
        {
            // Corrupt or locked file — treat as empty
        }

        return set;
    }

    public static List<Car> LoadCarsFromCsv(string filePath)
    {
        var list = new List<Car>();
        if (!File.Exists(filePath))
            return list;

        var propsByName = typeof(Car).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        try
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            var headerLine = reader.ReadLine();
            if (headerLine is null)
                return list;

            var header = ParseCsvLine(headerLine);
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var fields = ParseCsvLine(line);
                var car = new Car();
                for (var j = 0; j < header.Count; j++)
                {
                    var name = header[j].Trim();
                    if (string.IsNullOrEmpty(name) || !propsByName.TryGetValue(name, out var prop))
                        continue;
                    var raw = j < fields.Count ? fields[j] : null;
                    TrySetCarProperty(car, prop, raw);
                }

                if (!string.IsNullOrWhiteSpace(car.auctionUniqueNumber))
                    list.Add(car);
            }
        }
        catch
        {
            return list;
        }

        return list;
    }

    private static void TrySetCarProperty(Car car, PropertyInfo prop, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            prop.SetValue(car, null);
            return;
        }

        raw = raw.Trim();
        var pt = prop.PropertyType;

        if (pt == typeof(string))
        {
            prop.SetValue(car, raw);
            return;
        }

        if (pt == typeof(object))
        {
            prop.SetValue(car, raw);
            return;
        }

        if (pt == typeof(int?))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                prop.SetValue(car, (int?)v);
            return;
        }

        if (pt == typeof(double?))
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                prop.SetValue(car, (double?)v);
            return;
        }

        if (pt == typeof(bool?))
        {
            if (bool.TryParse(raw, out var v))
                prop.SetValue(car, (bool?)v);
            return;
        }

        if (pt == typeof(DateTime?))
        {
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var v))
                prop.SetValue(car, (DateTime?)v);
        }
    }

    private static string FormatCell(object? value)
    {
        if (value is null)
            return string.Empty;

        switch (value)
        {
            case string s:
                return NormalizeSingleLine(s);
            case DateTime dt:
                return dt.ToString("o", CultureInfo.InvariantCulture);
            case bool b:
                return b ? "True" : "False";
            case JsonElement je:
                return NormalizeSingleLine(je.GetRawText());
            case IEnumerable en when value is not string:
                return NormalizeSingleLine(string.Join(", ",
                    en.Cast<object>().Select(FormatCell)));
            case IFormattable f:
                return NormalizeSingleLine(f.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty);
            default:
                return NormalizeSingleLine(value.ToString() ?? string.Empty);
        }
    }

    private static string NormalizeSingleLine(string s) =>
        s.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');

    private static string EscapeField(string value)
    {
        value = NormalizeSingleLine(value);
        var mustQuote = value.Contains(Delimiter) || value.Contains('"');
        if (!mustQuote)
            return value;
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                        inQuotes = false;
                }
                else
                    sb.Append(c);
            }
            else
            {
                if (c == Delimiter)
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"')
                    inQuotes = true;
                else
                    sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }

    public static void Save(IReadOnlyList<Car> items, string filePath)
    {
        var properties = CarPropertiesOrdered.Value;
        using var writer = new StreamWriter(filePath, false, Utf8NoBom);
        writer.WriteLine(string.Join(Delimiter.ToString(), properties.Select(p => EscapeField(p.Name))));
        foreach (var item in items)
        {
            var cells = properties.Select(p => EscapeField(FormatCell(p.GetValue(item))));
            writer.WriteLine(string.Join(Delimiter.ToString(), cells));
        }

        Console.WriteLine($"Dane zapisane do {Path.GetFileName(filePath)} ({items.Count} wierszy).");
    }

    public static void AppendNewCars(string filePath, IReadOnlyList<Car> newCars)
    {
        if (newCars.Count == 0)
        {
            Console.WriteLine("Brak nowych ofert do dopisania do pliku.");
            return;
        }

        var existing = File.Exists(filePath) ? LoadCarsFromCsv(filePath) : [];
        var merged = new List<Car>(existing.Count + newCars.Count);
        merged.AddRange(existing);
        merged.AddRange(newCars);
        Save(merged, filePath);
    }
}
