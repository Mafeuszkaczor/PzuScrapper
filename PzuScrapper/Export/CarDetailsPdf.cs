using System.Globalization;
using PzuScrapper.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PzuScrapper.Export;

internal static class CarDetailsPdf
{
    private static readonly CultureInfo PlCulture = new("pl-PL");

    private static readonly string AccentColor = Colors.Blue.Darken3;
    private static readonly string MutedColor = Colors.Grey.Darken1;
    private static readonly string FaintColor = Colors.Grey.Medium;
    private static readonly string BorderColor = Colors.Grey.Lighten2;
    private static readonly string SectionBg = Colors.Grey.Lighten4;

    // Zdjęcia są osadzane jako miniatury (2 na wiersz ~260pt = 92mm szerokości).
    // 96 DPI wystarcza do oglądania na ekranie, Medium = agresywna, ale czytelna kompresja JPEG.
    private const int PhotoRasterDpi = 96;
    private const ImageCompressionQuality PhotoCompressionQuality = ImageCompressionQuality.Medium;

    // ─── Public API ────────────────────────────────────────────────────────

    public static bool TryWrite(string outputPath, CarDetails d, string? photosDirectory)
    {
        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(9.5f));

                    page.Header().Element(h => RenderHeader(h, d));
                    page.Content().Column(main =>
                    {
                        main.Spacing(12);
                        RenderSummary(main, d);
                        RenderVehicle(main, d);
                        RenderEngineAndGearbox(main, d);
                        RenderMileage(main, d);
                        RenderBody(main, d);
                        RenderHistory(main, d);
                        RenderTechnicalState(main, d);
                        RenderDamages(main, d);
                        RenderRepairCosts(main, d);
                        RenderEquipment(main, d);
                        RenderPhotos(main, photosDirectory);
                    });
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8).FontColor(FaintColor));
                        t.Span("Strona ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            }).GeneratePdf(outputPath);

            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            PzuScrapper.Log.Error("PDF", $"Generowanie PDF nie powiodło się: {ex.Message}");
            return false;
        }
    }

    public static void TryDeletePhotoDirectory(string? photosDirectory)
    {
        if (string.IsNullOrWhiteSpace(photosDirectory) || !Directory.Exists(photosDirectory))
            return;
        try { Directory.Delete(photosDirectory, recursive: true); }
        catch (Exception ex)
        {
            PzuScrapper.Log.Warn("PDF", $"Nie udało się usunąć katalogu zdjęć: {ex.Message}");
        }
    }

    // ─── Sections ──────────────────────────────────────────────────────────

    private static void RenderHeader(IContainer container, CarDetails d)
    {
        container.PaddingBottom(10).Column(h =>
        {
            h.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text($"{d.Manufacturer} {d.Model}".Trim())
                        .SemiBold().FontSize(16);
                    if (!string.IsNullOrWhiteSpace(d.Vin))
                        left.Item().PaddingTop(2).Text($"VIN: {d.Vin}")
                            .FontSize(9).FontColor(MutedColor);
                });
                row.ConstantItem(180).AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text("Numer oferty")
                        .FontSize(8).FontColor(FaintColor).LetterSpacing(0.08f);
                    right.Item().AlignRight().Text(d.AuctionUniqueNumber ?? "—")
                        .SemiBold().FontSize(14);
                });
            });
            h.Item().PaddingTop(8).LineHorizontal(0.75f).LineColor(BorderColor);
        });
    }

    private static void RenderSummary(ColumnDescriptor main, CarDetails d)
    {
        var bd = d.AuctionBasicData;
        var dmg = d.InsuranceDamageDetails;
        Section(main, "Podsumowanie oferty", col =>
        {
            KvGrid(col,
                ("Rozpoczęcie aukcji",         DateTimeText(d.AuctionStartDate)),
                ("Wygaśnięcie",                DateTimeText(bd?.ExpirationDate)),
                ("Data utworzenia",            DateTimeText(d.CreateDate)),
                ("Zgłoszenie szkody (ubezp.)", DateText(dmg?.InsuranceDamageCreateDate)),
                ("Lokalizacja",                Location(bd)),
                ("Stawka VAT",                 Desc(bd?.VatRate)),
                ("Kwota brutto przed szkodą",  Money(bd?.GrossAmountBeforeDamage)),
                ("Szacowana wartość resztkowa", Money(bd?.EstimatedGrossResidueAmount)));
        });
    }

    private static void RenderVehicle(ColumnDescriptor main, CarDetails d)
    {
        Section(main, "Dane pojazdu", col =>
        {
            KvGrid(col,
                ("Marka",              d.Manufacturer ?? "—"),
                ("Model",              d.Model ?? "—"),
                ("Wersja / typ",       d.Type ?? "—"),
                ("Rok produkcji",      d.ProductionYear?.ToString(PlCulture) ?? "—"),
                ("VIN",                d.Vin ?? "—"),
                ("Data rejestracji",   DateText(d.RegistrationDate)),
                ("Badanie techniczne", DateText(d.TechnicalExaminationDate)));
        });
    }

    private static void RenderEngineAndGearbox(ColumnDescriptor main, CarDetails d)
    {
        var e = d.Engine;
        Section(main, "Silnik i napęd", col =>
        {
            KvGrid(col,
                ("Typ silnika",     Desc(e?.Type)),
                ("Moc",             e?.Power is null ? "—" : $"{e.Power} {Desc(e.PowerUnitMeasurement)}"),
                ("Pojemność",       e?.Capacity is null ? "—" : $"{e.Capacity.Value.ToString("N0", PlCulture)} {Desc(e.CapacityUnitMeasurement)}"),
                ("Skrzynia biegów", Desc(d.GearboxType)));
        });
    }

    private static void RenderMileage(ColumnDescriptor main, CarDetails d)
    {
        var m = d.Mileage;
        Section(main, "Przebieg", col =>
        {
            KvGrid(col,
                ("Przebieg",    m?.Value is null ? "—" : $"{m.Value.Value.ToString("N0", PlCulture)} {Desc(m.UnitMeasurement)}"),
                ("Typ pomiaru", Desc(m?.MeasurementType)));
        });
    }

    private static void RenderBody(ColumnDescriptor main, CarDetails d)
    {
        Section(main, "Nadwozie i wyposażenie bazowe", col =>
        {
            KvGrid(col,
                ("Typ nadwozia", Desc(d.VehicleBodyType)),
                ("Lakier",       Desc(d.VehiclePaintType)),
                ("Liczba drzwi", d.DoorsNumber?.ToString(PlCulture) ?? "—"),
                ("Kluczyki",     Desc(d.KeysNumber)));
        });
    }

    private static void RenderHistory(ColumnDescriptor main, CarDetails d)
    {
        Section(main, "Historia i dokumenty", col =>
        {
            KvGrid(col,
                ("Pierwszy właściciel", Desc(d.FirstOwner)),
                ("Import indywidualny", Desc(d.PrivatelyImported)),
                ("Karta pojazdu",       Desc(d.VehicleCard)),
                ("Książka serwisowa",   Desc(d.ServiceBookAvailable)),
                ("Współwłasność",       Desc(d.SharedOwnership)),
                ("Leasing",             Desc(d.Leasing)),
                ("Obciążenie / zastaw", Desc(d.BankLien)),
                ("Cesja",               Desc(d.Cession)),
                ("Odliczenie VAT",      Desc(d.VatDeduction)));
        });
    }

    private static void RenderTechnicalState(ColumnDescriptor main, CarDetails d)
    {
        Section(main, "Stan techniczny", col =>
        {
            KvGrid(col,
                ("Na kołach / toczenie",      Desc(d.Rollable)),
                ("Prowadzi",                  Desc(d.Drivable)),
                ("Silnik sprawny",            Desc(d.EngineWorking)),
                ("Poduszki uszkodzone",       Desc(d.AirbagDamaged)),
                ("Przeznaczenie do kasacji",  Desc(d.ToCassation)));
        });
    }

    private static void RenderDamages(ColumnDescriptor main, CarDetails d)
    {
        var hasRisk = d.AdditionalDamageRisk is not null
            || !string.IsNullOrWhiteSpace(d.AdditionalDamageRiskDescription);
        var hasRepair = !string.IsNullOrWhiteSpace(d.DamageDescriptionRepair);
        var hasReplaced = !string.IsNullOrWhiteSpace(d.DamageDescriptionReplacedElements);

        if (!hasRisk && !hasRepair && !hasReplaced) return;

        Section(main, "Uszkodzenia i naprawa", col =>
        {
            if (hasRisk)
            {
                col.Item().PaddingBottom(4).Text(t =>
                {
                    t.Span("Dodatkowe ryzyko uszkodzenia: ").FontColor(MutedColor);
                    t.Span(Desc(d.AdditionalDamageRisk)).SemiBold();
                    if (!string.IsNullOrWhiteSpace(d.AdditionalDamageRiskDescription))
                        t.Span($" — {d.AdditionalDamageRiskDescription.Trim()}");
                });
            }
            if (hasRepair) RenderBulletList(col, "Zakres naprawy", d.DamageDescriptionRepair!);
            if (hasReplaced) RenderBulletList(col, "Wymienione elementy", d.DamageDescriptionReplacedElements!);
        });
    }

    private static void RenderRepairCosts(ColumnDescriptor main, CarDetails d)
    {
        var r = d.RepairCosts;
        if (r is null) return;

        Section(main, "Koszty naprawy", col =>
        {
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(1);
                    c.RelativeColumn(1);
                });

                t.Header(h =>
                {
                    h.Cell().Element(TableHeaderCell).Text("Pozycja");
                    h.Cell().Element(TableHeaderCell).AlignRight().Text("Netto");
                    h.Cell().Element(TableHeaderCell).AlignRight().Text("Brutto");
                });

                void Row(string name, CostItem? c, bool total = false)
                {
                    Func<IContainer, IContainer> style = total ? TableTotalCell : TableCell;
                    t.Cell().Element(style).Text(name);
                    t.Cell().Element(style).AlignRight().Text(Money(c?.NetCost));
                    t.Cell().Element(style).AlignRight().Text(Money(c?.GrossCost));
                }

                Row("Części oryginalne",     r.OriginalParts);
                Row("Części zamienne",       r.AlternativeParts);
                Row("Suma części",           r.ReplacementPartsSubtotal);
                Row("Robocizna blacharska",  r.TinsmithLabor);
                Row("Robocizna lakiernicza", r.PainterLabor);
                Row("Materiały lakiernicze", r.PaintMaterials);
                Row("Materiały dodatkowe",   r.AdditionalMaterials);
                Row("RAZEM",                 r.TotalCost, total: true);
            });
        });
    }

    private static void RenderEquipment(ColumnDescriptor main, CarDetails d)
    {
        if (d.Equipments is null || d.Equipments.Count == 0) return;

        var items = d.Equipments
            .Select(e => e.Description?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToList();
        if (items.Count == 0) return;

        Section(main, "Wyposażenie dodatkowe", col =>
        {
            var half = (items.Count + 1) / 2;
            var left = items.Take(half).ToList();
            var right = items.Skip(half).ToList();

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    foreach (var i in left)
                        c.Item().PaddingBottom(1).Text($"•  {i}").FontSize(9);
                });
                row.ConstantItem(14);
                row.RelativeItem().Column(c =>
                {
                    foreach (var i in right)
                        c.Item().PaddingBottom(1).Text($"•  {i}").FontSize(9);
                });
            });
        });
    }

    private static void RenderPhotos(ColumnDescriptor main, string? photosDirectory)
    {
        var photos = GetOrderedPhotoPaths(photosDirectory);
        if (photos.Count == 0) return;

        Section(main, "Zdjęcia", col =>
        {
            for (var i = 0; i < photos.Count; i += 2)
            {
                col.Item().PaddingBottom(8).Row(row =>
                {
                    RenderPhotoCell(row.RelativeItem(), photos[i]);
                    row.ConstantItem(8);
                    if (i + 1 < photos.Count)
                        RenderPhotoCell(row.RelativeItem(), photos[i + 1]);
                    else
                        row.RelativeItem();
                });
            }
        });
    }

    private static void RenderPhotoCell(IContainer cell, string photoPath)
    {
        cell.Padding(1).Border(0.5f).BorderColor(BorderColor)
            .Image(photoPath)
            .WithCompressionQuality(PhotoCompressionQuality)
            .WithRasterDpi(PhotoRasterDpi)
            .FitWidth();
    }

    // ─── Layout primitives ─────────────────────────────────────────────────

    private static void Section(ColumnDescriptor main, string title, Action<ColumnDescriptor> content)
    {
        main.Item().Column(col =>
        {
            col.Item().Background(SectionBg).PaddingVertical(5).PaddingHorizontal(8)
                .Text(title).SemiBold().FontSize(11).FontColor(AccentColor);
            col.Item().PaddingTop(6).PaddingHorizontal(2).Column(content);
        });
    }

    private static void KvGrid(ColumnDescriptor col, params (string Label, string Value)[] pairs)
    {
        var half = (pairs.Length + 1) / 2;
        var left = pairs.Take(half).ToArray();
        var right = pairs.Skip(half).ToArray();

        col.Item().Row(row =>
        {
            row.RelativeItem().Column(l => RenderKvList(l, left));
            row.ConstantItem(18);
            row.RelativeItem().Column(r => RenderKvList(r, right));
        });
    }

    private static void RenderKvList(ColumnDescriptor col, IEnumerable<(string L, string V)> pairs)
    {
        foreach (var (l, v) in pairs)
        {
            col.Item().PaddingBottom(3).Row(row =>
            {
                row.RelativeItem(2).PaddingRight(6).Text(l)
                    .FontColor(MutedColor).FontSize(8.5f);
                row.RelativeItem(3).Text(string.IsNullOrWhiteSpace(v) ? "—" : v)
                    .FontSize(9.5f);
            });
        }
    }

    private static void RenderBulletList(ColumnDescriptor col, string title, string semicolonSeparated)
    {
        var items = semicolonSeparated
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length == 0) return;

        col.Item().PaddingTop(4).Text(title).SemiBold().FontSize(10);
        col.Item().PaddingTop(2).PaddingLeft(6).Column(c =>
        {
            foreach (var item in items)
                c.Item().PaddingBottom(1).Text($"•  {item}").FontSize(9);
        });
    }

    // ─── Table cell styles ─────────────────────────────────────────────────

    private static IContainer TableHeaderCell(IContainer c) =>
        c.Background(SectionBg).BorderBottom(0.5f).BorderColor(BorderColor)
         .PaddingVertical(5).PaddingHorizontal(6)
         .DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(AccentColor));

    private static IContainer TableCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(BorderColor)
         .PaddingVertical(3).PaddingHorizontal(6)
         .DefaultTextStyle(x => x.FontSize(9));

    private static IContainer TableTotalCell(IContainer c) =>
        c.BorderTop(1).BorderBottom(1).BorderColor(Colors.Grey.Medium)
         .PaddingVertical(4).PaddingHorizontal(6)
         .DefaultTextStyle(x => x.SemiBold().FontSize(9.5f));

    // ─── Formatting helpers ────────────────────────────────────────────────

    private static string DateText(DateTimeOffset? d) =>
        d is null ? "—" : d.Value.ToString("dd.MM.yyyy", PlCulture);

    private static string DateTimeText(DateTimeOffset? d) =>
        d is null ? "—" : d.Value.ToString("dd.MM.yyyy HH:mm", PlCulture);

    private static string Money(double? v) =>
        v is null ? "—" : v.Value.ToString("N2", PlCulture) + " PLN";

    private static string Location(AuctionBasicData? bd)
    {
        if (bd?.Location is null) return "—";
        var parts = new[] { bd.Location.PostalCode, Desc(bd.Location.Country) }
            .Where(s => !string.IsNullOrWhiteSpace(s) && s != "—");
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? "—" : joined;
    }

    private static string Desc(CodeDescription? cd) =>
        string.IsNullOrWhiteSpace(cd?.Description) ? "—" : cd.Description!;

    private static string Desc(CatalogEntry? ce) =>
        string.IsNullOrWhiteSpace(ce?.Description) ? "—" : ce.Description!;

    private static List<string> GetOrderedPhotoPaths(string? photosDirectory)
    {
        if (string.IsNullOrWhiteSpace(photosDirectory) || !Directory.Exists(photosDirectory))
            return [];
        return Directory.EnumerateFiles(photosDirectory, "*.jpg", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
    }
}
