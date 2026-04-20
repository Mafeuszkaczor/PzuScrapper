namespace PzuScrapper.Models.Request;

/// <summary>Optional auction list filters (production year, vehicle category).</summary>
public sealed class BidderSearchFiltersRequest
{
    public string? ProductionYearFrom { get; init; }
    public string? ProductionYearTo { get; init; }
    public IReadOnlyList<string>? VehicleCategoryCodes { get; init; }
}
