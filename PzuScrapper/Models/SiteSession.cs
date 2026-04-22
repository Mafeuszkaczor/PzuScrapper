namespace PzuScrapper.Models;

public sealed class SiteSession
{
    public required string Login { get; init; }
    public required string Password { get; init; }
    public string? SessionJson { get; init; }
}
