namespace PzuScrapper.Auctions;

/// <summary>Optional auction list filters (production year, vehicle category).</summary>
public sealed class BidderSearchFilters
{
    public string? ProductionYearFrom { get; init; }
    public string? ProductionYearTo { get; init; }
    public IReadOnlyList<string>? VehicleCategoryCodes { get; init; }
}
