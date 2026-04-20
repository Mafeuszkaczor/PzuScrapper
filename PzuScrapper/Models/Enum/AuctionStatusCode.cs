namespace Models.Enum;

/// <summary>
/// Auction status in the search API (values match JSON).
/// </summary>
public enum AuctionStatusCode
{
    STARTED,
    OVERTIME,
    FINISHED,
    OVERTIME_FINISHED,
}
