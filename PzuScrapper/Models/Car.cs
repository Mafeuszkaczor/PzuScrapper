namespace PzuScrapper.Models;

/// <summary>Flattened row from the auction search API (list view).</summary>
public sealed class Car
{
    public string? StatusDescription { get; set; }
    public string? AuctionUniqueNumber { get; set; }
    public string? AuctionType { get; set; }
    public string? PostalCode { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? CurrencyCode { get; set; }
    public string? EngineType { get; set; }
    public int? CapacityValue { get; set; }
    public int? PowerValue { get; set; }
    public int? MileageValue { get; set; }
    public string? Gearbox { get; set; }
    public string? PrivatelyImported { get; set; }
    public string? ToCassation { get; set; }
    public object? PropertyName { get; set; }
    public object? Quantity { get; set; }
    public string? Type { get; set; }
    public object? PropertyDamageType { get; set; }
    public string? AuctionTypeCode { get; set; }
    public string? BiddingTypeDescription { get; set; }
    public string? AuctionItemCode { get; set; }
    public string? StatusCode { get; set; }
    public string? CapacityUnitMeasurment { get; set; }
    public string? PowerUnitMeasurment { get; set; }
    public string? CountryCode { get; set; }
    public string? MileageUnitMeasurment { get; set; }
    public double? TotalRepairCost { get; set; }
    public string? FirstOwner { get; set; }
    public string? VatDeduction { get; set; }
    public string? Leasing { get; set; }
    public string? AirbagDamaged { get; set; }
    public string? Drivable { get; set; }
    public string? Rollable { get; set; }
    public string? EngineWorking { get; set; }
    public object? PropertyUnitMeasurment { get; set; }
    public object? SerialNumber { get; set; }
    public object? Description { get; set; }
    public object? RepairDescription { get; set; }
    public string? DamageDescriptionReplacedElements { get; set; }
    public string? DamageDescriptionRepair { get; set; }
    public string? Comments { get; set; }
    public int? MainImageId { get; set; }
    public string? MainImageFilename { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? AuctionStartDate { get; set; }
    public int? ProductionYear { get; set; }
    public int? GrossAmountBeforeDamage { get; set; }
    public object? MyOffer { get; set; }
    public int? EstimatedGrossResidueAmount { get; set; }
    public int? RelatedOffersCount { get; set; }
    public bool? CanCreateOffer { get; set; }
    public bool? CanPrintAuction { get; set; }
    public object? BestOfferValue { get; set; }
    public bool? AnyRelatedPermittedOffer { get; set; }
}
