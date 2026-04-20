using System;

namespace PzuScrapper.Models.Car;

public class AuctionBasicData
    {
        public Location location { get; set; }
        public VatRate vatRate { get; set; }
        public BiddingType biddingType { get; set; }
        public Currency currency { get; set; }
        public Market market { get; set; }
        public int grossAmountBeforeDamage { get; set; }
        public int estimatedGrossResidueAmount { get; set; }
        public ExternalSystem externalSystem { get; set; }
        public AuctionItem auctionItem { get; set; }
        public DateTime expirationDate { get; set; }
    }

