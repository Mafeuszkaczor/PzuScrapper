using System.Globalization;
using PzuScrapper.Models;

namespace PzuScrapper.Search;

// ─── Console prompt ────────────────────────────────────────────────────────

/// <summary>Interactive auction search filter setup (writes to console).</summary>
internal static class BidderSearchFiltersPrompt
{
    private const int MinProductionYear = 1950;

    public static BidderSearchFiltersRequest Read()
    {
        Console.WriteLine("\n *** Jeśli nie chcesz używać filtra nie wpisuj wartości i zatwierdź enterem ***");

        int? minYear;
        int? maxYear;
        while (true)
        {
            minYear = ReadOptionalProductionYear("[Filtry] Podaj minimalny rok produkcji (np. 2017, Enter = brak)");
            maxYear = ReadOptionalProductionYear("[Filtry] Podaj maksymalny rok produkcji (np. 2020, Enter = brak)");

            if (minYear.HasValue && maxYear.HasValue && minYear.Value > maxYear.Value)
            {
                Log.Warn("Filtry", "Minimalny rok nie może być większy od maksymalnego. Wpisz oba ponownie.\n");
                continue;
            }
            break;
        }

        var categoryEntries = VehicleCategoryCodes.Catalog
            .OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Select(kv => (Code: kv.Key, Description: kv.Value))
            .ToList();

        return new BidderSearchFiltersRequest
        {
            ProductionYearFrom = minYear?.ToString(CultureInfo.InvariantCulture),
            ProductionYearTo = maxYear?.ToString(CultureInfo.InvariantCulture),
            VehicleCategoryCodes = ReadVehicleCategoryChoice(categoryEntries),
        };
    }

    private static int? ReadOptionalProductionYear(string prompt)
    {
        var maxYear = DateTime.Now.Year + 1;
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(prompt);
            var raw = ReadLineWithArrow();
            if (string.IsNullOrEmpty(raw))
                return null;

            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var y)
                || y < MinProductionYear
                || y > maxYear)
            {
                Log.Warn("Filtry", $"Niepoprawny rok. Podaj liczbę od {MinProductionYear} do {maxYear}, albo Enter aby pominąć.");
                continue;
            }
            return y;
        }
    }

    private static IReadOnlyList<string>? ReadVehicleCategoryChoice(
        IReadOnlyList<(string Code, string Description)> categoryEntries)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("[Filtry] Wybierz typ pojazdu — numer z listy, 0 lub Enter = brak filtra");
            for (var i = 0; i < categoryEntries.Count; i++)
                Console.WriteLine($" {i + 1} — {categoryEntries[i].Description}");

            var raw = ReadLineWithArrow();
            if (string.IsNullOrEmpty(raw) || raw == "0")
                return null;

            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                Log.Warn("Filtry", "Wpisz liczbę (numer z listy, 0 lub Enter).");
                continue;
            }

            if (n < 1 || n > categoryEntries.Count)
            {
                Log.Warn("Filtry", $"Wybierz numer od 1 do {categoryEntries.Count}, albo 0/Enter aby pominąć kategorię.");
                continue;
            }

            return new[] { categoryEntries[n - 1].Code };
        }
    }

    private static string ReadLineWithArrow()
    {
        Console.Write("> ");
        Console.Out.Flush();
        return (Console.ReadLine() ?? string.Empty).Trim();
    }
}

// ─── Request factory ───────────────────────────────────────────────────────

/// <summary>Builds the POST body for the bidder auction search API.</summary>
internal static class BidderSearchRequestFactory
{
    private const int PageSize = 20;

    public static SearchRequest Create(int pageNumber, BidderSearchFiltersRequest? filters = null)
    {
        var now = DateTime.UtcNow;
        var expirationFrom = DateTime.SpecifyKind(now.AddMonths(-3).Date, DateTimeKind.Utc);
        var creationTo = DateTime.SpecifyKind(now.AddHours(-2), DateTimeKind.Utc);

        var request = new SearchRequest
        {
            AuctionItemCode = AuctionItemCode.VEHICLE,
            Pageable = new Pageable
            {
                Page = pageNumber,
                Size = PageSize,
                Sort = new Sort
                {
                    Order = new List<Order>
                    {
                        new() { Name = "auctionStartDate", Direction = "DESC" },
                    },
                },
            },
            Page = pageNumber,
            AuctionStatusCodes = new List<AuctionStatusCode>
            {
                AuctionStatusCode.STARTED,
                AuctionStatusCode.OVERTIME,
            },
            ExpirationDateFrom = expirationFrom,
            CreationDateTo = creationTo,
        };

        if (filters is null)
            return request;

        if (!string.IsNullOrWhiteSpace(filters.ProductionYearFrom))
            request.ProductionYearFrom = filters.ProductionYearFrom.Trim();
        if (!string.IsNullOrWhiteSpace(filters.ProductionYearTo))
            request.ProductionYearTo = filters.ProductionYearTo.Trim();
        if (filters.VehicleCategoryCodes is { Count: > 0 })
            request.VehicleCategoryCodes = filters.VehicleCategoryCodes.ToList();

        return request;
    }
}
