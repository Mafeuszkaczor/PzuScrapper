using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PzuScrapper.Auctions;

/// <summary>
/// ISO 8601 UTC as in the browser (e.g. 2026-04-09T18:57:00.000Z), without arbitrary fractional digit count from DateTime.
/// </summary>
internal sealed class UtcIsoDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (string.IsNullOrEmpty(s))
            return default;
        return DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utc.ToString(Format, CultureInfo.InvariantCulture));
    }
}
