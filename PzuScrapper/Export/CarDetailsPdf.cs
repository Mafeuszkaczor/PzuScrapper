using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using PzuScrapper.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PzuScrapper.Export;

internal static class CarDetailsPdf
{
    private static readonly BindingFlags PropFlags = BindingFlags.Public | BindingFlags.Instance;

    // ─── Public API ────────────────────────────────────────────────────────

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
                            AppendObject(dataCol, details, "", isRoot: true));

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

    // ─── Rendering helpers ─────────────────────────────────────────────────

    private static void AppendObject(ColumnDescriptor column, object? obj, string path, bool isRoot)
    {
        if (obj is null) return;

        var type = obj.GetType();

        if (type == typeof(JsonElement)) { AppendRow(column, path, JsonValue((JsonElement)obj)); return; }
        if (obj is string s) { AppendRow(column, path, s); return; }
        if (IsSimpleDisplay(type)) { AppendRow(column, path, FormatScalar(obj, type)); return; }
        if (obj is IEnumerable en && obj is not string) { AppendEnumerable(column, path, en); return; }

        foreach (var prop in type.GetProperties(PropFlags).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!prop.CanRead) continue;
            var name = prop.Name;
            var fullPath = string.IsNullOrEmpty(path) ? name : $"{path}.{name}";

            if (ShouldSkip(fullPath, name, isRoot)) continue;

            var value = prop.GetValue(obj);
            if (value is null) continue;

            var label = LabelMap.GetLabel(name, fullPath);

            if (IsSimpleDisplay(value.GetType()) || value is string || value is JsonElement)
            {
                AppendRow(column, fullPath, $"{label}: {FormatValue(value, name)}");
                continue;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                column.Item().PaddingTop(4).Text(label).SemiBold();
                AppendEnumerable(column, fullPath, enumerable);
                continue;
            }

            column.Item().PaddingTop(6).Text(label).SemiBold().FontSize(10);
            column.Item().PaddingLeft(8).Column(nested => AppendObject(nested, value, fullPath, isRoot: false));
        }
    }

    private static void AppendEnumerable(ColumnDescriptor column, string path, IEnumerable enumerable)
    {
        var idx = 0;
        foreach (var item in enumerable)
        {
            idx++;
            if (item is null) continue;

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
            column.Item().PaddingLeft(8).Column(nested =>
                AppendObject(nested, item, $"{path}[{idx}]", isRoot: false));
        }
    }

    private static void AppendRow(ColumnDescriptor column, string _, string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
            column.Item().PaddingBottom(2).Text(line).WrapAnywhere(false);
    }

    private static bool ShouldSkip(string fullPath, string propName, bool isRoot)
    {
        if (LabelMap.ExcludedPaths.Contains(fullPath)) return true;
        if (isRoot && LabelMap.ExcludedRootProperties.Contains(propName)) return true;
        if (!isRoot && LabelMap.ExcludedNestedPropertyNames.Contains(propName)) return true;
        return false;
    }

    private static readonly CultureInfo PlCulture = new("pl-PL");

    private static bool IsSimpleDisplay(System.Type type) =>
        type.IsPrimitive || type.IsEnum
        || type == typeof(decimal) || type == typeof(DateTime)
        || type == typeof(DateTimeOffset) || type == typeof(Guid)
        || Nullable.GetUnderlyingType(type) != null;

    private static string FormatScalar(object obj, System.Type type, string? propName = null)
    {
        var u = Nullable.GetUnderlyingType(type) ?? type;
        if (u == typeof(DateTime))
            return ((DateTime)obj).ToString("dd.MM.yyyy HH:mm", PlCulture);
        if (u == typeof(DateTimeOffset))
            return ((DateTimeOffset)obj).ToString("dd.MM.yyyy HH:mm", PlCulture);
        if (u == typeof(bool))
            return (bool)obj ? "Tak" : "Nie";
        if ((u == typeof(double) || u == typeof(float) || u == typeof(decimal))
            && propName != null && LabelMap.MonetaryPropertyNames.Contains(propName))
            return Convert.ToDouble(obj).ToString("N2", PlCulture) + " PLN";
        if (u == typeof(double) || u == typeof(float) || u == typeof(decimal))
            return Convert.ToDecimal(obj).ToString("G", PlCulture);
        return Convert.ToString(obj, PlCulture) ?? "";
    }

    private static string FormatValue(object value, string? propName = null)
    {
        if (value is null) return "—";
        if (value is JsonElement je) return JsonValue(je);
        var t = value.GetType();
        if (IsSimpleDisplay(t) || value is string) return FormatScalar(value, t, propName);
        var desc = value.GetType().GetProperty("description", PropFlags)?.GetValue(value) as string;
        return !string.IsNullOrWhiteSpace(desc) ? desc : value.ToString() ?? "—";
    }

    private static string JsonValue(JsonElement je) => je.ValueKind switch
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

    // ─── Label map (was CarDetailsPdfLabelMap) ─────────────────────────────

    private static class LabelMap
    {
        internal static readonly HashSet<string> MonetaryPropertyNames = new(StringComparer.Ordinal)
        {
            "netCost",
            "grossCost",
            "grossAmountBeforeDamage",
            "estimatedGrossResidueAmount",
        };

        internal static readonly HashSet<string> ExcludedRootProperties = new(StringComparer.Ordinal)
        {
            nameof(CarDetails.id),
            nameof(CarDetails.auctionType),
            nameof(CarDetails.status),
            nameof(CarDetails.cancelReason),
            nameof(CarDetails.attachments),
            nameof(CarDetails.allowAutoCloseBeforeExpiration),
            nameof(CarDetails.externalId),
            nameof(CarDetails.relatedOffersCount),
            nameof(CarDetails.determiningBestOffer),
            nameof(CarDetails.vehicleCategory),
        };

        internal static readonly HashSet<string> ExcludedPaths = new(StringComparer.Ordinal)
        {
            "auctionBasicData.biddingType",
            "auctionBasicData.externalSystem",
        };

        internal static readonly HashSet<string> ExcludedNestedPropertyNames = new(StringComparer.Ordinal)
        {
            "id",
            "code",
            "active",
        };

        private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
        {
            ["auctionUniqueNumber"] = "Numer oferty (unikalny)",
            ["createDate"] = "Data utworzenia oferty",
            ["auctionStartDate"] = "Data rozpoczęcia aukcji",
            ["productionYear"] = "Rok produkcji",
            ["sharedOwnership"] = "Współwłasność",
            ["leasing"] = "Leasing",
            ["bankLien"] = "Obciążenie bankowe / zastaw",
            ["cession"] = "Cesja",
            ["vatDeduction"] = "Odliczenie VAT",
            ["auctionBasicData"] = "Podstawowe dane aukcji",
            ["insuranceDamageDetails"] = "Dane szkody ubezpieczeniowej",
            ["manufacturer"] = "Marka pojazdu",
            ["model"] = "Model",
            ["type"] = "Wersja / typ pojazdu",
            ["comments"] = "Uwagi",
            ["additionalDamageRisk"] = "Dodatkowe ryzyko uszkodzenia",
            ["additionalDamageRiskDescription"] = "Opis dodatkowego ryzyka",
            ["bestOffer"] = "Najlepsza oferta",
            ["retailMargin"] = "Marża detaliczna",
            ["canRegisterComplaint"] = "Możliwość złożenia reklamacji",
            ["vin"] = "Numer VIN",
            ["registrationDate"] = "Data pierwszej rejestracji",
            ["technicalExaminationDate"] = "Data przeglądu technicznego",
            ["engine"] = "Silnik",
            ["mileage"] = "Przebieg",
            ["gearboxType"] = "Skrzynia biegów",
            ["vehiclePaintType"] = "Rodzaj lakieru",
            ["vehicleBodyType"] = "Typ nadwozia",
            ["doorsNumber"] = "Liczba drzwi",
            ["keysNumber"] = "Liczba kluczyków",
            ["privatelyImported"] = "Import indywidualny",
            ["firstOwner"] = "Pierwszy właściciel",
            ["vehicleCard"] = "Karta pojazdu",
            ["serviceBookAvailable"] = "Książka serwisowa",
            ["equipments"] = "Wyposażenie dodatkowe",
            ["missingDocuments"] = "Brakujące dokumenty",
            ["rollable"] = "Stan 'na kołach' / możliwość toczenia",
            ["airbagDamaged"] = "Stan poduszek powietrznych",
            ["drivable"] = "Możliwość prowadzenia pojazdu",
            ["engineWorking"] = "Stan silnika (sprawność)",
            ["toCassation"] = "Przeznaczenie do kasacji",
            ["damageDescriptionRepair"] = "Opis uszkodzeń (naprawa)",
            ["damageDescriptionReplacedElements"] = "Opis wymienionych elementów",
            ["repairCosts"] = "Koszty naprawy",
            ["damageZones"] = "Strefy uszkodzeń",
            ["description"] = "Opis",
            ["location"] = "Lokalizacja",
            ["postalCode"] = "Kod pocztowy",
            ["country"] = "Kraj",
            ["vatRate"] = "Stawka VAT",
            ["currency"] = "Waluta",
            ["market"] = "Rynek",
            ["grossAmountBeforeDamage"] = "Kwota brutto przed uszkodzeniem",
            ["estimatedGrossResidueAmount"] = "Szacowana kwota brutto resztkowa",
            ["auctionItem"] = "Przedmiot aukcji",
            ["expirationDate"] = "Data wygaśnięcia oferty",
            ["ecCode"] = "Kod EC",
            ["insuranceDamageCreateDate"] = "Data zgłoszenia szkody",
            ["power"] = "Moc",
            ["powerUnitMeasurement"] = "Jednostka mocy",
            ["capacity"] = "Pojemność",
            ["capacityUnitMeasurement"] = "Jednostka pojemności",
            ["noEngineAndGearboxData"] = "Brak danych silnika/skrzyni",
            ["value"] = "Wartość przebiegu",
            ["unitMeasurement"] = "Jednostka",
            ["measurementType"] = "Typ pomiaru",
            ["noMileageData"] = "Brak danych o przebiegu",
            ["netCost"] = "Kwota netto",
            ["grossCost"] = "Kwota brutto",
            ["originalParts"] = "Części oryginalne",
            ["alternativeParts"] = "Części zamienne",
            ["replacementPartsSubtotal"] = "Suma części zamiennych",
            ["tinsmithLabor"] = "Robocizna blacharska",
            ["painterLabor"] = "Robocizna lakiernicza",
            ["paintMaterials"] = "Materiały lakiernicze",
            ["additionalMaterials"] = "Materiały dodatkowe",
            ["totalCost"] = "Suma całkowita",
        };

        internal static string GetLabel(string propertyName, string? fallbackPath = null) =>
            Labels.TryGetValue(propertyName, out var l)
                ? l
                : (fallbackPath != null && Labels.TryGetValue(fallbackPath, out var l2) ? l2 : propertyName);
    }
}
