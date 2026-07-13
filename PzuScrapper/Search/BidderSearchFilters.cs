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
            Console.WriteLine("[Filtry] Wybierz typ(y) pojazdu — numery oddzielone spacją (np. 9 8 4).");
            Console.WriteLine("         0 lub Enter = brak filtra (wszystkie typy).");
            for (var i = 0; i < categoryEntries.Count; i++)
                Console.WriteLine($" {i + 1} — {categoryEntries[i].Description}");

            var raw = ReadLineWithArrow();
            if (string.IsNullOrEmpty(raw) || raw == "0")
                return null;

            var tokens = raw.Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var selected = new List<string>();
            var seen = new HashSet<int>();
            var valid = true;

            foreach (var token in tokens)
            {
                if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                {
                    Log.Warn("Filtry", $"„{token}” to nie jest liczba. Wpisz tylko numery z listy oddzielone spacją.");
                    valid = false;
                    break;
                }

                if (n < 1 || n > categoryEntries.Count)
                {
                    Log.Warn("Filtry", $"Numer {n} poza listą. Dozwolone: 1–{categoryEntries.Count}.");
                    valid = false;
                    break;
                }

                if (seen.Add(n))
                    selected.Add(categoryEntries[n - 1].Code);
            }

            if (!valid)
                continue;

            if (selected.Count == 0)
                return null;

            var chosenNames = selected
                .Select(code => categoryEntries.First(e => e.Code == code).Description);
            Log.Info("Filtry", $"Wybrane typy: {string.Join(", ", chosenNames)}.");
            return selected;
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
