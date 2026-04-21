// Search API request/response DTOs, pagination helpers, and enums.
// All in the root Models namespace to match Car.cs.
namespace Models;

// ─── Pagination ────────────────────────────────────────────────────────────

public class Order
{
    public string name { get; set; }
    public string direction { get; set; }
}

public class Sort
{
    public List<Order> order { get; set; }
}

public class Pageable
{
    public int page { get; set; }
    public int size { get; set; }
    public Sort sort { get; set; }
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

public class SearchRequest
{
    public Pageable pageable { get; set; }
    public AuctionItemCode auctionItemCode { get; set; }
    public DateTime expirationDateFrom { get; set; }
    public object expirationDateTo { get; set; }
    public string? productionYearFrom { get; set; }
    public string? productionYearTo { get; set; }
    public object auctionUniqueNumber { get; set; }
    public object externalInsuranceDamageId { get; set; }
    public object postalCode { get; set; }
    public List<string>? manufacturerCodes { get; set; }
    public List<string>? modelCodes { get; set; }
    public List<AuctionStatusCode> auctionStatusCodes { get; set; }
    public object auctionTypeCodes { get; set; }
    public object offerStatusCodes { get; set; }
    public object auctioneerCodes { get; set; }
    public object auctionStartDateFrom { get; set; }
    public object auctionStartDateTo { get; set; }
    public object distance { get; set; }
    public object privatelyImportedCode { get; set; }
    public string? leasingCode { get; set; }
    public string? vatDeductionCode { get; set; }
    public object existOfferComplaint { get; set; }
    public string? relatedOffersCountFrom { get; set; }
    public string? relatedOffersCountTo { get; set; }
    public object bidderCodes { get; set; }
    public object vehicleCategoryCodes { get; set; }
    public object engineTypeCodes { get; set; }
    public object mileageFrom { get; set; }
    public object mileageTo { get; set; }
    public object gearboxTypeCodes { get; set; }
    public object propertyCategoryCodes { get; set; }
    public object propertyName { get; set; }
    public string? biddingAuction { get; set; }
    public object offerUniqueNumber { get; set; }
    public object offerExpirationDateFrom { get; set; }
    public object offerExpirationDateTo { get; set; }
    public object offerExpirationDate { get; set; }
    public object advancedMode { get; set; }
    public object sortBy { get; set; }
    public int page { get; set; }
    public DateTime creationDateTo { get; set; }
}

// ─── API response envelope ─────────────────────────────────────────────────

public class SearchResponse
{
    public int total { get; set; }
    public int pageNumber { get; set; }
    public int pageSize { get; set; }
    public int totalPages { get; set; }
    public List<Car> result { get; set; }
}
