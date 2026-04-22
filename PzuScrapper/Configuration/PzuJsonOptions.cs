using System.Text.Json;
using System.Text.Json.Serialization;
using PzuScrapper.Search;

namespace PzuScrapper.Configuration;

/// <summary>
/// Shared JSON options — API uses camelCase on the wire,
/// we use PascalCase in .NET, and this bridges both directions.
/// </summary>
internal static class PzuJsonOptions
{
    /// <summary>For reading API responses into our DTOs.</summary>
    public static readonly JsonSerializerOptions Read = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>For building request bodies sent to the API (enums as strings, UTC ISO dates).</summary>
    public static readonly JsonSerializerOptions Write = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
            new UtcIsoDateTimeConverter(),
        },
    };
}
