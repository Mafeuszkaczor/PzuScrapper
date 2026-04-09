namespace PzuScrapper.Configuration;

/// <summary>Fixed paths for the PPO bidder portal.</summary>
internal static class PzuPortalUrls
{
    public static string VehicleSaleDetails(string auctionUniqueNumber) =>
        $"https://ppo.pzu.pl/bidder/auction/details/vs/{auctionUniqueNumber}/vehicle-sale";
}
