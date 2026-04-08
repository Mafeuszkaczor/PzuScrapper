using Models;

namespace PzuScrapper.Auctions;

internal static class BidderSearchRequestFactory
{
    public static SearchRequest Create(int pageNumber) => new()
    {
        pageable = new Pageable
        {
            page = pageNumber,
            size = 20,
            sort = new Sort { order = new List<Order>() }
        },
        page = pageNumber,
        auctionStatusCodes = new List<string> { "ACTIVE" },
        manufacturerCodes = new List<string>(),
        modelCodes = new List<string>(),
        biddingAuction = "Y",
        leasingCode = "",
        vatDeductionCode = "",
        relatedOffersCountFrom = "",
        relatedOffersCountTo = "",
        productionYearFrom = "",
        productionYearTo = "",
        expirationDateFrom = DateTime.UtcNow,
        creationDateTo = DateTime.UtcNow.AddYears(1),
    };
}
