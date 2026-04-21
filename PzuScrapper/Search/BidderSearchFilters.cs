using System.Globalization;
using Models;

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
                Console.WriteLine(
                    "[Filtry] Minimalny rok nie może być większy od maksymalnego. Wpisz oba ponownie.\n");
                continue;
            }

            break;
        }

        var categoryEntries = VehicleCategoryCodes.Catalog
            .OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Select(kv => (Code: kv.Key, Description: kv.Value))
            .ToList();

        var vehicleCategoryCodes = ReadVehicleCategoryChoice(categoryEntries);

        return new BidderSearchFiltersRequest
        {
            ProductionYearFrom = minYear?.ToString(CultureInfo.InvariantCulture),
            ProductionYearTo = maxYear?.ToString(CultureInfo.InvariantCulture),
            VehicleCategoryCodes = vehicleCategoryCodes,
        };
    }

    private static int? ReadOptionalProductionYear(string prompt)
    {
        var maxYear = DateTime.Now.Year + 1;
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(prompt);
            var raw = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(raw))
                return null;

            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var y)
                || y < MinProductionYear
                || y > maxYear)
            {
                Console.WriteLine(
                    $"[Filtry] Niepoprawny rok. Podaj liczbę całkowitą od {MinProductionYear} do {maxYear}, albo Enter aby pominąć.");
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
            Console.Out.Flush();

            var raw = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(raw))
                return null;
            if (raw == "0")
                return null;

            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                Console.WriteLine("[Filtry] Wpisz liczbę (numer z listy, 0 lub Enter).");
                continue;
            }

            if (n < 1 || n > categoryEntries.Count)
            {
                Console.WriteLine(
                    $"[Filtry] Wybierz numer od 1 do {categoryEntries.Count}, albo 0 / Enter aby pominąć kategorię.");
                continue;
            }

            return new[] { categoryEntries[n - 1].Code };
        }
    }
}

// ─── Request factory ───────────────────────────────────────────────────────

/// <summary>Builds the POST body for the bidder auction search API.</summary>
internal static class BidderSearchRequestFactory
{
    public static SearchRequest Create(int pageNumber, BidderSearchFiltersRequest? filters = null)
    {
        var now = DateTime.UtcNow;
        var threeMonthsAgo = now.AddMonths(-3);
        var expirationFrom = new DateTime(
            threeMonthsAgo.Year, threeMonthsAgo.Month, threeMonthsAgo.Day,
            0, 0, 0, DateTimeKind.Utc);

        var rawCreationTo = now.AddHours(-2);
        var creationTo = new DateTime(
            rawCreationTo.Year, rawCreationTo.Month, rawCreationTo.Day,
            rawCreationTo.Hour, rawCreationTo.Minute, rawCreationTo.Second,
            DateTimeKind.Utc);

        var request = new SearchRequest
        {
            auctionItemCode = AuctionItemCode.VEHICLE,
            pageable = new Pageable
            {
                page = pageNumber,
                size = 20,
                sort = new Sort
                {
                    order = new List<Order>
                    {
                        new() { name = "auctionStartDate", direction = "DESC" },
                    },
                },
            },
            page = pageNumber,
            auctionStatusCodes = new List<AuctionStatusCode>
            {
                AuctionStatusCode.STARTED,
                AuctionStatusCode.OVERTIME,
            },
            expirationDateFrom = expirationFrom,
            creationDateTo = creationTo,
        };

        if (filters is null)
            return request;

        if (!string.IsNullOrWhiteSpace(filters.ProductionYearFrom))
            request.productionYearFrom = filters.ProductionYearFrom.Trim();
        if (!string.IsNullOrWhiteSpace(filters.ProductionYearTo))
            request.productionYearTo = filters.ProductionYearTo.Trim();
        if (filters.VehicleCategoryCodes is { Count: > 0 })
            request.vehicleCategoryCodes = filters.VehicleCategoryCodes.ToList();

        return request;
    }
}
