namespace PzuScrapper.Models;

// ─── Reusable shapes for API enum-like values ─────────────────────────────

/// <summary>Short dictionary value from the API: { code, description }.</summary>
public sealed class CodeDescription
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

/// <summary>Catalog entry with id + active flag: { id, code, description, active }.</summary>
public sealed class CatalogEntry
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; }
}

/// <summary>Repair cost line item — both net and gross values, either may be missing.</summary>
public sealed class CostItem
{
    public double? NetCost { get; set; }
    public double? GrossCost { get; set; }
}

// ─── Composite types ──────────────────────────────────────────────────────

public sealed class Equipment
{
    public string? Description { get; set; }
}

public sealed class Location
{
    public string? PostalCode { get; set; }
    public CatalogEntry? Country { get; set; }
}

public sealed class InsuranceDamageDetails
{
    public object? EcCode { get; set; }
    public DateTimeOffset? InsuranceDamageCreateDate { get; set; }
}

public sealed class Engine
{
    public CatalogEntry? Type { get; set; }
    public int? Power { get; set; }
    public CodeDescription? PowerUnitMeasurement { get; set; }
    public int? Capacity { get; set; }
    public CodeDescription? CapacityUnitMeasurement { get; set; }
    public bool NoEngineAndGearboxData { get; set; }
}

public sealed class Mileage
{
    public int? Value { get; set; }
    public CodeDescription? UnitMeasurement { get; set; }
    public CodeDescription? MeasurementType { get; set; }
    public bool NoMileageData { get; set; }
}

public sealed class AuctionBasicData
{
    public Location? Location { get; set; }
    public CatalogEntry? VatRate { get; set; }
    public CodeDescription? BiddingType { get; set; }
    public CatalogEntry? Currency { get; set; }
    public CatalogEntry? Market { get; set; }
    public double? GrossAmountBeforeDamage { get; set; }
    public double? EstimatedGrossResidueAmount { get; set; }
    public CatalogEntry? ExternalSystem { get; set; }
    public CodeDescription? AuctionItem { get; set; }
    public DateTimeOffset? ExpirationDate { get; set; }
}

public sealed class RepairCosts
{
    public CostItem? OriginalParts { get; set; }
    public CostItem? AlternativeParts { get; set; }
    public CostItem? ReplacementPartsSubtotal { get; set; }
    public CostItem? TinsmithLabor { get; set; }
    public CostItem? PainterLabor { get; set; }
    public CostItem? PaintMaterials { get; set; }
    public CostItem? AdditionalMaterials { get; set; }
    public CostItem? TotalCost { get; set; }
}

// ─── Root DTO ─────────────────────────────────────────────────────────────

public sealed class CarDetails
{
    public int Id { get; set; }
    public string? AuctionUniqueNumber { get; set; }
    public CatalogEntry? AuctionType { get; set; }
    public CodeDescription? Status { get; set; }
    public DateTimeOffset? CreateDate { get; set; }
    public DateTimeOffset? AuctionStartDate { get; set; }
    public int? ProductionYear { get; set; }
    public CodeDescription? SharedOwnership { get; set; }
    public CodeDescription? Leasing { get; set; }
    public CodeDescription? BankLien { get; set; }
    public CodeDescription? Cession { get; set; }
    public CodeDescription? VatDeduction { get; set; }
    public AuctionBasicData? AuctionBasicData { get; set; }
    public InsuranceDamageDetails? InsuranceDamageDetails { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Type { get; set; }
    public string? Comments { get; set; }
    public object? CancelReason { get; set; }
    public List<object>? Attachments { get; set; }
    public bool AllowAutoCloseBeforeExpiration { get; set; }
    public object? ExternalId { get; set; }
    public CodeDescription? AdditionalDamageRisk { get; set; }
    public string? AdditionalDamageRiskDescription { get; set; }
    public object? BestOffer { get; set; }
    public int RelatedOffersCount { get; set; }
    public string? DeterminingBestOffer { get; set; }
    public object? RetailMargin { get; set; }
    public bool CanRegisterComplaint { get; set; }
    public string? Vin { get; set; }
    public CatalogEntry? VehicleCategory { get; set; }
    public DateTimeOffset? RegistrationDate { get; set; }
    public DateTimeOffset? TechnicalExaminationDate { get; set; }
    public Engine? Engine { get; set; }
    public Mileage? Mileage { get; set; }
    public CodeDescription? GearboxType { get; set; }
    public CatalogEntry? VehiclePaintType { get; set; }
    public CatalogEntry? VehicleBodyType { get; set; }
    public int? DoorsNumber { get; set; }
    public CodeDescription? KeysNumber { get; set; }
    public CodeDescription? PrivatelyImported { get; set; }
    public CodeDescription? FirstOwner { get; set; }
    public CodeDescription? VehicleCard { get; set; }
    public CodeDescription? ServiceBookAvailable { get; set; }
    public List<Equipment>? Equipments { get; set; }
    public object? MissingDocuments { get; set; }
    public CodeDescription? Rollable { get; set; }
    public CodeDescription? AirbagDamaged { get; set; }
    public CodeDescription? Drivable { get; set; }
    public CodeDescription? EngineWorking { get; set; }
    public CodeDescription? ToCassation { get; set; }
    public string? DamageDescriptionRepair { get; set; }
    public string? DamageDescriptionReplacedElements { get; set; }
    public RepairCosts? RepairCosts { get; set; }
    public List<object>? DamageZones { get; set; }
}
