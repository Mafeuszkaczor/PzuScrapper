using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using PzuScrapper.Models.Car;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PzuScrapper.Export;

internal static class CarDetailsPdfWriter
{
    private static readonly BindingFlags PropFlags = BindingFlags.Public | BindingFlags.Instance;

    public static bool TryWrite(string outputPath, CarDetails details, string? photosDirectory)
    {
        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(9.5f));
                    page.Header().Column(h =>
                    {
                        h.Item().Text($"{details.manufacturer} {details.model}".Trim())
                            .SemiBold().FontSize(15);
                        h.Item().PaddingTop(4).Text($"Numer oferty: {details.auctionUniqueNumber ?? "—"}");
                        if (!string.IsNullOrWhiteSpace(details.vin))
                            h.Item().Text($"VIN: {details.vin}");
                    });

                    page.Content().Column(main =>
                    {
                        main.Item().PaddingTop(8).Text("Dane pojazdu").Bold().FontSize(12);
                        main.Item().PaddingTop(6).Column(dataCol =>
                        {
                            AppendObject(dataCol, details, "", isRoot: true);
                        });

                        var imgs = GetOrderedPhotoPaths(photosDirectory);
                        if (imgs.Count > 0)
                        {
                            main.Item().PaddingTop(18).Text("Zdjęcia").Bold().FontSize(12);
                            foreach (var imgPath in imgs)
                            {
                                main.Item().PaddingTop(10).Column(photoCol =>
                                {
                                    photoCol.Item().Text(Path.GetFileName(imgPath)).FontColor(Colors.Grey.Medium);
                                    photoCol.Item().PaddingTop(4).Image(imgPath).FitWidth();
                                });
                            }
                        }
                    });
                });
            }).GeneratePdf(outputPath);

            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF] Błąd generowania PDF: {ex.Message}");
            return false;
        }
    }

    private static void AppendObject(ColumnDescriptor column, object? obj, string path, bool isRoot)
    {
        if (obj is null)
            return;

        var type = obj.GetType(); // System.Type

        if (type == typeof(JsonElement))
        {
            AppendRow(column, path, JsonValue((JsonElement)obj));
            return;
        }

        if (obj is string s)
        {
            AppendRow(column, path, s);
            return;
        }

        if (IsSimpleDisplay(type))
        {
            AppendRow(column, path, FormatScalar(obj, type));
            return;
        }

        if (obj is IEnumerable enumerable && obj is not string)
        {
            AppendEnumerable(column, path, enumerable);
            return;
        }

        foreach (var prop in type.GetProperties(PropFlags).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!prop.CanRead)
                continue;

            var name = prop.Name;
            var fullPath = string.IsNullOrEmpty(path) ? name : $"{path}.{name}";

            if (ShouldSkip(fullPath, name, isRoot))
                continue;

            var value = prop.GetValue(obj);
            if (value is null)
                continue;

            var label = CarDetailsPdfLabelMap.GetLabel(name, fullPath);

            if (IsSimpleDisplay(value.GetType()) || value is string || value is JsonElement)
            {
                AppendRow(column, fullPath, $"{label}: {FormatValue(value)}");
                continue;
            }

            if (value is IEnumerable en && value is not string)
            {
                column.Item().PaddingTop(4).Text(label).SemiBold();
                AppendEnumerable(column, fullPath, en);
                continue;
            }

            column.Item().PaddingTop(6).Text(label).SemiBold().FontSize(10);
            column.Item().PaddingLeft(8).Column(nested => { AppendObject(nested, value, fullPath, isRoot: false); });
        }
    }

    private static void AppendEnumerable(ColumnDescriptor column, string path, IEnumerable enumerable)
    {
        var idx = 0;
        foreach (var item in enumerable)
        {
            idx++;
            if (item is null)
                continue;

            if (item is Equipment eq)
            {
                if (!string.IsNullOrWhiteSpace(eq.description))
                    AppendRow(column, $"{path}[{idx}]", eq.description);
                continue;
            }

            if (IsSimpleDisplay(item.GetType()) || item is string)
            {
                AppendRow(column, $"{path}[{idx}]", FormatValue(item));
                continue;
            }

            column.Item().PaddingTop(4).Text($"{path} [{idx}]").SemiBold();
            column.Item().PaddingLeft(8).Column(nested => { AppendObject(nested, item, $"{path}[{idx}]", isRoot: false); });
        }
    }

    private static void AppendRow(ColumnDescriptor column, string pathForDebug, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        column.Item().PaddingBottom(2).Text(line).WrapAnywhere(false);
    }

    private static bool ShouldSkip(string fullPath, string propName, bool isRoot)
    {
        if (CarDetailsPdfLabelMap.ExcludedPaths.Contains(fullPath))
            return true;
        if (isRoot && CarDetailsPdfLabelMap.ExcludedRootProperties.Contains(propName))
            return true;
        if (!isRoot && CarDetailsPdfLabelMap.ExcludedNestedPropertyNames.Contains(propName))
            return true;
        return false;
    }

    private static bool IsSimpleDisplay(System.Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(Guid)
        || Nullable.GetUnderlyingType(type) != null;

    private static string FormatScalar(object obj, System.Type type)
    {
        var u = Nullable.GetUnderlyingType(type) ?? type;
        if (u == typeof(DateTime))
            return ((DateTime)obj).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (u == typeof(bool))
            return (bool)obj ? "Tak" : "Nie";
        if (u == typeof(double) || u == typeof(float) || u == typeof(decimal))
            return Convert.ToDecimal(obj).ToString(CultureInfo.InvariantCulture);
        return Convert.ToString(obj, CultureInfo.InvariantCulture) ?? "";
    }

    private static string FormatValue(object value)
    {
        if (value is null)
            return "—";
        if (value is JsonElement je)
            return JsonValue(je);
        var t = value.GetType();
        if (IsSimpleDisplay(t) || value is string)
            return FormatScalar(value, t);
        var desc = TryGetDescription(value);
        if (!string.IsNullOrWhiteSpace(desc))
            return desc;
        return value.ToString() ?? "—";
    }

    private static string? TryGetDescription(object o)
    {
        var p = o.GetType().GetProperty("description", PropFlags);
        if (p?.GetValue(o) is string d && !string.IsNullOrWhiteSpace(d))
            return d;
        return null;
    }

    private static string JsonValue(JsonElement je) =>
        je.ValueKind switch
        {
            JsonValueKind.String => je.GetString() ?? "",
            JsonValueKind.Number => je.GetRawText(),
            JsonValueKind.True => "Tak",
            JsonValueKind.False => "Nie",
            JsonValueKind.Null => "—",
            _ => je.GetRawText()
        };

    private static List<string> GetOrderedPhotoPaths(string? photosDirectory)
    {
        if (string.IsNullOrWhiteSpace(photosDirectory) || !Directory.Exists(photosDirectory))
            return [];

        return Directory.EnumerateFiles(photosDirectory, "*.jpg", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Usuwa katalog roboczy ze zdjęciami po udanym PDF.</summary>
    public static void TryDeletePhotoDirectory(string? photosDirectory)
    {
        if (string.IsNullOrWhiteSpace(photosDirectory) || !Directory.Exists(photosDirectory))
            return;
        try
        {
            Directory.Delete(photosDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF] Nie udało się usunąć katalogu zdjęć: {ex.Message}");
        }
    }
}
