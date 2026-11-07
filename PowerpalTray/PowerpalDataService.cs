using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PowerpalTray;

internal static class PowerpalDataService
{
    private static readonly string[] CandidatePaths = { "/sensor/power", "/sensor/Power" };

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(2),
    };

    private static string? _lastWorkingPath;

    public static async Task<PowerpalReading> FetchAsync(string boardAddress)
    {
        var pathsToTry = _lastWorkingPath is { } known
            ? new[] { known, CandidatePaths[0] == known ? CandidatePaths[1] : CandidatePaths[0] }
            : CandidatePaths;

        string? lastError = null;

        foreach (var path in pathsToTry)
        {
            var url = boardAddress.TrimEnd('/') + path;
            try
            {
                using var response = await Http.GetAsync(url).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    lastError = "HTTP 404";
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return new PowerpalReading { Error = $"HTTP {(int)response.StatusCode}" };

                _lastWorkingPath = path;
                return ParseBody(body);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new PowerpalReading { Error = lastError ?? "unreachable" };
    }

    private static PowerpalReading ParseBody(string rawBody)
    {
        var body = Regex.Replace(rawBody, @":\s*NaN\b", ":null", RegexOptions.IgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var valueElement))
            {
                double? watts = valueElement.ValueKind switch
                {
                    JsonValueKind.Number => valueElement.GetDouble(),
                    JsonValueKind.String when double.TryParse(
                        valueElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    _ => null,
                };

                if (watts is { } w && !double.IsNaN(w))
                    return new PowerpalReading { Kilowatts = w / 1000.0 };
            }

            return new PowerpalReading { HasNoReadingYet = true };
        }
        catch (JsonException)
        {
            return new PowerpalReading { Error = "bad response" };
        }
    }
}
