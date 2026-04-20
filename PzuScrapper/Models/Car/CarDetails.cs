using System;
using System.Collections.Generic;

namespace PzuScrapper.Models.Car;

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

