using System.Reflection;
using System.Text;

namespace PzuScrapper.Export;

internal static class CsvExporter
{
    private const char Separator = ';';

    public static void Save<T>(IReadOnlyList<T> items, string filePath)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(Separator, properties.Select(p => p.Name)));

        foreach (var item in items)
        {
            var values = properties.Select(p =>
            {
                var raw = p.GetValue(item)?.ToString() ?? string.Empty;
                return raw.Contains(Separator) || raw.Contains('"') || raw.Contains('\n')
                    ? $"\"{raw.Replace("\"", "\"\"")}\""
                    : raw;
            });
            sb.AppendLine(string.Join(Separator, values));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"Dane zapisane do {filePath} ({items.Count} wierszy).");
    }
}
