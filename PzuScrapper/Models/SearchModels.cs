namespace PzuScrapper.Models;

// ─── Pagination ────────────────────────────────────────────────────────────

public sealed class Order
{
    public string? Name { get; set; }
    public string? Direction { get; set; }
}

public sealed class Sort
{
    public List<Order>? Order { get; set; }
}

public sealed class Pageable
{
    public int Page { get; set; }
    public int Size { get; set; }
    public Sort? Sort { get; set; }
}

// ─── Enums ─────────────────────────────────────────────────────────────────

public enum AuctionItemCode
{
    VEHICLE,
    PROPERTY,
}

/// <summary>Auction lifecycle values used in the search filter.</summary>
public enum AuctionStatusCode
{
    STARTED,
    OVERTIME,
    FINISHED,
    OVERTIME_FINISHED,
}

// ─── User-facing search filters (console prompt → factory) ─────────────────

/// <summary>Optional filters selected by the user before scraping starts.</summary>
public sealed class BidderSearchFiltersRequest
{
    public string? ProductionYearFrom { get; init; }
    public string? ProductionYearTo { get; init; }
    public IReadOnlyList<string>? VehicleCategoryCodes { get; init; }
}

// ─── API request body ──────────────────────────────────────────────────────

public sealed class SearchRequest
{
    public Pageable? Pageable { get; set; }
    public AuctionItemCode AuctionItemCode { get; set; }
    public DateTime ExpirationDateFrom { get; set; }
    public object? ExpirationDateTo { get; set; }
    public string? ProductionYearFrom { get; set; }
    public string? ProductionYearTo { get; set; }
    public object? AuctionUniqueNumber { get; set; }
    public object? ExternalInsuranceDamageId { get; set; }
    public object? PostalCode { get; set; }
    public List<string>? ManufacturerCodes { get; set; }
    public List<string>? ModelCodes { get; set; }
    public List<AuctionStatusCode>? AuctionStatusCodes { get; set; }
    public object? AuctionTypeCodes { get; set; }
    public object? OfferStatusCodes { get; set; }
    public object? AuctioneerCodes { get; set; }
    public object? AuctionStartDateFrom { get; set; }
    public object? AuctionStartDateTo { get; set; }
    public object? Distance { get; set; }
    public object? PrivatelyImportedCode { get; set; }
    public string? LeasingCode { get; set; }
    public string? VatDeductionCode { get; set; }
    public object? ExistOfferComplaint { get; set; }
    public string? RelatedOffersCountFrom { get; set; }
    public string? RelatedOffersCountTo { get; set; }
    public object? BidderCodes { get; set; }
    public List<string>? VehicleCategoryCodes { get; set; }
    public object? EngineTypeCodes { get; set; }
    public object? MileageFrom { get; set; }
    public object? MileageTo { get; set; }
    public object? GearboxTypeCodes { get; set; }
    public object? PropertyCategoryCodes { get; set; }
    public object? PropertyName { get; set; }
    public string? BiddingAuction { get; set; }
    public object? OfferUniqueNumber { get; set; }
    public object? OfferExpirationDateFrom { get; set; }
    public object? OfferExpirationDateTo { get; set; }
    public object? OfferExpirationDate { get; set; }
    public object? AdvancedMode { get; set; }
    public object? SortBy { get; set; }
    public int Page { get; set; }
    public DateTime CreationDateTo { get; set; }
}

// ─── API response envelope ─────────────────────────────────────────────────

public sealed class SearchResponse
{
    public int Total { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<Car>? Result { get; set; }
}
