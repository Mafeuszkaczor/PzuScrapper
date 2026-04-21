namespace PzuScrapper.Models;

// ─── Primitive lookup/catalog shapes ──────────────────────────────────────

public class EngineType  // was "Type" — renamed to avoid System.Type ambiguity
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class AuctionType
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class Country
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class Currency
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class ExternalSystem
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class Market
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class VatRate
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class VehicleBodyType
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class VehicleCategory
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

public class VehiclePaintType
{
    public int id { get; set; }
    public string code { get; set; }
    public string description { get; set; }
    public bool active { get; set; }
}

// ─── Code/description pairs ────────────────────────────────────────────────

public class AdditionalDamageRisk   { public string code { get; set; } public string description { get; set; } }
public class AirbagDamaged          { public string code { get; set; } public string description { get; set; } }
public class AuctionItem            { public string code { get; set; } public string description { get; set; } }
public class BankLien               { public string code { get; set; } public string description { get; set; } }
public class BiddingType            { public string code { get; set; } public string description { get; set; } }
public class CapacityUnitMeasurement{ public string code { get; set; } public string description { get; set; } }
public class Cession                { public string code { get; set; } public string description { get; set; } }
public class Drivable               { public string code { get; set; } public string description { get; set; } }
public class EngineWorking          { public string code { get; set; } public string description { get; set; } }
public class FirstOwner             { public string code { get; set; } public string description { get; set; } }
public class GearboxType            { public string code { get; set; } public string description { get; set; } }
public class KeysNumber             { public string code { get; set; } public string description { get; set; } }
public class Leasing                { public string code { get; set; } public string description { get; set; } }
public class MeasurementType        { public string code { get; set; } public string description { get; set; } }
public class PowerUnitMeasurement   { public string code { get; set; } public string description { get; set; } }
public class PrivatelyImported      { public string code { get; set; } public string description { get; set; } }
public class Rollable               { public string code { get; set; } public string description { get; set; } }
public class ServiceBookAvailable   { public string code { get; set; } public string description { get; set; } }
public class SharedOwnership        { public string code { get; set; } public string description { get; set; } }
public class Status                 { public string code { get; set; } public string description { get; set; } }
public class ToCassation            { public string code { get; set; } public string description { get; set; } }
public class UnitMeasurement        { public string code { get; set; } public string description { get; set; } }
public class VatDeduction           { public string code { get; set; } public string description { get; set; } }
public class VehicleCard            { public string code { get; set; } public string description { get; set; } }

// ─── Cost buckets ──────────────────────────────────────────────────────────

public class AdditionalMaterials    { public double netCost { get; set; } public double grossCost { get; set; } }
public class AlternativeParts       { public double netCost { get; set; } public double grossCost { get; set; } }
public class OriginalParts          { public double netCost { get; set; } public double grossCost { get; set; } }
public class PainterLabor           { public double netCost { get; set; } public double grossCost { get; set; } }
public class PaintMaterials         { public double netCost { get; set; } public double grossCost { get; set; } }
public class ReplacementPartsSubtotal { public double netCost { get; set; } public double grossCost { get; set; } }
public class TinsmithLabor          { public double netCost { get; set; } public double grossCost { get; set; } }
public class TotalCost              { public double netCost { get; set; } public double grossCost { get; set; } }

// ─── Composite types ──────────────────────────────────────────────────────

public class Equipment { public string description { get; set; } }

public class Location
{
    public string postalCode { get; set; }
    public Country country { get; set; }
}

public class InsuranceDamageDetails
{
    public object ecCode { get; set; }
    public DateTime insuranceDamageCreateDate { get; set; }
}

public class Engine
{
    public EngineType type { get; set; }
    public int power { get; set; }
    public PowerUnitMeasurement powerUnitMeasurement { get; set; }
    public int capacity { get; set; }
    public CapacityUnitMeasurement capacityUnitMeasurement { get; set; }
    public bool noEngineAndGearboxData { get; set; }
}

public class Mileage
{
    public int value { get; set; }
    public UnitMeasurement unitMeasurement { get; set; }
    public MeasurementType measurementType { get; set; }
    public bool noMileageData { get; set; }
}

public class AuctionBasicData
{
    public Location location { get; set; }
    public VatRate vatRate { get; set; }
    public BiddingType biddingType { get; set; }
    public Currency currency { get; set; }
    public Market market { get; set; }
    public double grossAmountBeforeDamage { get; set; }
    public double estimatedGrossResidueAmount { get; set; }
    public ExternalSystem externalSystem { get; set; }
    public AuctionItem auctionItem { get; set; }
    public DateTime expirationDate { get; set; }
}

public class RepairCosts
{
    public OriginalParts originalParts { get; set; }
    public AlternativeParts alternativeParts { get; set; }
    public ReplacementPartsSubtotal replacementPartsSubtotal { get; set; }
    public TinsmithLabor tinsmithLabor { get; set; }
    public PainterLabor painterLabor { get; set; }
    public PaintMaterials paintMaterials { get; set; }
    public AdditionalMaterials additionalMaterials { get; set; }
    public TotalCost totalCost { get; set; }
}

// ─── Root DTO ─────────────────────────────────────────────────────────────

public class CarDetails
{
    public int id { get; set; }
    public string auctionUniqueNumber { get; set; }
    public AuctionType auctionType { get; set; }
    public Status status { get; set; }
    public DateTime createDate { get; set; }
    public DateTime auctionStartDate { get; set; }
    public int productionYear { get; set; }
    public SharedOwnership sharedOwnership { get; set; }
    public Leasing leasing { get; set; }
    public BankLien bankLien { get; set; }
    public Cession cession { get; set; }
    public VatDeduction vatDeduction { get; set; }
    public AuctionBasicData auctionBasicData { get; set; }
    public InsuranceDamageDetails insuranceDamageDetails { get; set; }
    public string manufacturer { get; set; }
    public string model { get; set; }
    public string type { get; set; }
    public string comments { get; set; }
    public object cancelReason { get; set; }
    public List<object> attachments { get; set; }
    public bool allowAutoCloseBeforeExpiration { get; set; }
    public object externalId { get; set; }
    public AdditionalDamageRisk additionalDamageRisk { get; set; }
    public string additionalDamageRiskDescription { get; set; }
    public object bestOffer { get; set; }
    public int relatedOffersCount { get; set; }
    public string determiningBestOffer { get; set; }
    public object retailMargin { get; set; }
    public bool canRegisterComplaint { get; set; }
    public string vin { get; set; }
    public VehicleCategory vehicleCategory { get; set; }
    public DateTime registrationDate { get; set; }
    public object technicalExaminationDate { get; set; }
    public Engine engine { get; set; }
    public Mileage mileage { get; set; }
    public GearboxType gearboxType { get; set; }
    public VehiclePaintType vehiclePaintType { get; set; }
    public VehicleBodyType vehicleBodyType { get; set; }
    public int doorsNumber { get; set; }
    public KeysNumber keysNumber { get; set; }
    public PrivatelyImported privatelyImported { get; set; }
    public FirstOwner firstOwner { get; set; }
    public VehicleCard vehicleCard { get; set; }
    public ServiceBookAvailable serviceBookAvailable { get; set; }
    public List<Equipment> equipments { get; set; }
    public object missingDocuments { get; set; }
    public Rollable rollable { get; set; }
    public AirbagDamaged airbagDamaged { get; set; }
    public Drivable drivable { get; set; }
    public EngineWorking engineWorking { get; set; }
    public ToCassation toCassation { get; set; }
    public string damageDescriptionRepair { get; set; }
    public string damageDescriptionReplacedElements { get; set; }
    public RepairCosts repairCosts { get; set; }
    public List<object> damageZones { get; set; }
}
