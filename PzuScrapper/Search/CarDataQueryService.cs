using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PzuScrapper.Configuration;
using PzuScrapper.Models;

namespace PzuScrapper.Search;

internal sealed class CarDataQueryService
{
    private readonly HttpClient _http;

    public CarDataQueryService(HttpClient http) => _http = http;

    public async Task<CarDetails?> GetCarDataAsync(string carId, int retries = 2)
    {
        if (!int.TryParse(carId, out var parsedId))
        {
            Log.Error("API", $"Nieprawidłowy format ID pojazdu: {carId}");
            return null;
        }

        var relativeUrl = $"/api/auction/vehicle/sale/{parsedId}/bidder";

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromSeconds(attempt * 2);
                Log.Info("API", $"Ponawiam za {delay.TotalSeconds}s (próba {attempt + 1}/{retries + 1})…");
                await Task.Delay(delay);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);

                // Snapshot headers BEFORE SendAsync — afterwards HttpClient merges
                // DefaultRequestHeaders into request.Headers, which would duplicate them in the log.
                var headerSnapshot = CollectHeaders(request);

                using var response = await _http.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    await LogFailedRequestAsync(request, response, carId, headerSnapshot);
                    if ((int)response.StatusCode == 429 && attempt < retries)
                        continue;
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                try
                {
                    return JsonSerializer.Deserialize<CarDetails>(json, PzuJsonOptions.Read);
                }
                catch (JsonException ex)
                {
                    var snippet = json.Length > 200 ? json[..200] : json;
                    Log.Error("API", $"Błąd parsowania JSON dla {carId}: {ex.Message}");
                    Log.Info("API", $"Odpowiedź (fragment): {snippet}");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Warn("API", $"Problem sieciowy (próba {attempt + 1}): {ex.Message}");
                if (attempt == retries) return null;
            }
            catch (TaskCanceledException)
            {
                Log.Warn("API", $"Timeout dla oferty {carId} (próba {attempt + 1}).");
                if (attempt == retries) return null;
            }
        }

        return null;
    }

    // ─── Debug logging for failed requests ─────────────────────────────────

    private async Task LogFailedRequestAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        string carId,
        IReadOnlyList<(string Name, string Value)> headerSnapshot)
    {
        Log.Warn("API", $"HTTP {(int)response.StatusCode} dla oferty {carId}.");

        var absoluteUri = BuildAbsoluteUri(request);

        Log.Info("API", "--- Debug żądania ---");
        Log.Info("API", $"{request.Method} {absoluteUri}");
        foreach (var (name, value) in headerSnapshot)
            Log.Info("API", $"  {name}: {Redact(name, value)}");

        var body = await SafeReadBodyAsync(response);
        Log.Info("API", $"Response status: {(int)response.StatusCode} {response.ReasonPhrase}");
        Log.Info("API", $"Response body: {(string.IsNullOrEmpty(body) ? "(pusta)" : body)}");

        Log.Info("API", "curl:");
        Log.Info("API", BuildCurlCommand(request.Method.Method, absoluteUri, headerSnapshot));
        Log.Info("API", "---");
    }

    private Uri BuildAbsoluteUri(HttpRequestMessage request)
    {
        if (request.RequestUri is { IsAbsoluteUri: true } abs) return abs;
        var baseUri = _http.BaseAddress ?? new Uri("https://ppo.pzu.pl");
        return new Uri(baseUri, request.RequestUri!);
    }

    /// <summary>
    /// Must be called BEFORE SendAsync — afterwards HttpClient merges DefaultRequestHeaders
    /// into request.Headers and iterating both sources would show duplicates.
    /// </summary>
    private IReadOnlyList<(string Name, string Value)> CollectHeaders(HttpRequestMessage request)
    {
        var list = new List<(string, string)>();
        foreach (var h in _http.DefaultRequestHeaders)
            list.Add((h.Key, string.Join(", ", h.Value)));
        foreach (var h in request.Headers)
            list.Add((h.Key, string.Join(", ", h.Value)));
        return list;
    }

    private static string Redact(string headerName, string value)
    {
        // Keep bearer prefix + last few chars so we can verify the token rotated.
        if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && value.StartsWith("Bearer "))
        {
            var token = value[7..];
            var tail = token.Length > 12 ? token[^12..] : token;
            return $"Bearer …{tail} (len={token.Length})";
        }
        if (headerName.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
        {
            var names = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => p.Split('=')[0])
                .ToArray();
            return $"[{names.Length} ciasteczek: {string.Join(", ", names)}]";
        }
        return value;
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            return raw.Length > 500 ? raw[..500] + "…" : raw;
        }
        catch
        {
            return "(nie udało się odczytać)";
        }
    }

    private static string BuildCurlCommand(
        string method,
        Uri uri,
        IReadOnlyList<(string Name, string Value)> headers)
    {
        var sb = new StringBuilder();
        sb.Append("curl -X ").Append(method).Append(" \"").Append(uri).Append('"');
        foreach (var (name, value) in headers)
            sb.Append(" -H \"").Append(name).Append(": ").Append(value.Replace("\"", "\\\"")).Append('"');
        return sb.ToString();
    }
}
