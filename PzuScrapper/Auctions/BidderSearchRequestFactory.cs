using Models;
using Models.Enum;
using PzuScrapper.Models.Request;

namespace PzuScrapper.Auctions;

internal static class BidderSearchRequestFactory
{
    public static SearchRequest Create(int pageNumber, BidderSearchFiltersRequest? filters = null)
    {
        var now = DateTime.UtcNow;
        var threeMonthsAgo = now.AddMonths(-3);
        var expirationFrom = new DateTime(threeMonthsAgo.Year, threeMonthsAgo.Month, threeMonthsAgo.Day, 0, 0, 0, DateTimeKind.Utc);

        var rawCreationTo = now.AddHours(-2);
        var creationTo = new DateTime(rawCreationTo.Year, rawCreationTo.Month, rawCreationTo.Day, rawCreationTo.Hour, rawCreationTo.Minute, rawCreationTo.Second, DateTimeKind.Utc);

        var request = new SearchRequest
        {
            auctionItemCode = AuctionItemCode.VEHICLE,
            pageable = new Pageable
            {
                page = pageNumber,
                size = 20,
                sort = new Sort
                {
                    order = new List<Order>
                    {
                        new() { name = "auctionStartDate", direction = "DESC" },
                    },
                },
            },
            page = pageNumber,
            auctionStatusCodes = new List<AuctionStatusCode> { AuctionStatusCode.STARTED, AuctionStatusCode.OVERTIME },
            expirationDateFrom = expirationFrom,
            creationDateTo = creationTo,
        };

        if (filters is null)
            return request;

        if (!string.IsNullOrWhiteSpace(filters.ProductionYearFrom))
            request.productionYearFrom = filters.ProductionYearFrom.Trim();
        if (!string.IsNullOrWhiteSpace(filters.ProductionYearTo))
            request.productionYearTo = filters.ProductionYearTo.Trim();
        if (filters.VehicleCategoryCodes is { Count: > 0 })
            request.vehicleCategoryCodes = filters.VehicleCategoryCodes.ToList();

        return request;
    }
}
