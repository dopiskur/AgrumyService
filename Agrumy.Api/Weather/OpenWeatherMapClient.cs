using System.Globalization;
using System.Text.Json;

namespace Agrumy.Api.Weather
{
    /// Abstraction over the actual HTTP call, so WeatherEvaluator's own logic is unit-testable with a mock - same shape as Agrumy.Api.Firmware.IFirmwareFetcher.
    public interface IWeatherForecastClient
    {
        /// Null means the request failed (already logged) - the caller must leave the last known state alone rather than treat a failed fetch as "no rain".
        Task<double?> GetMaxRainProbabilityPercentAsync(double lat, double lon, string apiKey, CancellationToken ct);

        /// The coldest forecast bucket within the lookahead window, with the cloudiness/wind reading from that same bucket - null means the request failed (already logged), same "leave the last state alone" convention as GetMaxRainProbabilityPercentAsync.
        Task<FrostForecastResult?> GetFrostForecastAsync(double lat, double lon, string apiKey, int lookaheadHours, CancellationToken ct);

        /// The nearest forecast bucket (closest available reading to "now") - feeds WeatherEvaluator's TenantWeatherState.Outdoor* fields, which RuleConditionEvaluator exposes as SensorMetric.OutdoorTemperature/OutdoorHumidity/OutdoorWind. Null means the request failed (already logged), same "leave the last state alone" convention as the other two methods.
        Task<OutdoorConditions?> GetCurrentOutdoorConditionsAsync(double lat, double lon, string apiKey, CancellationToken ct);
    }

    /// HoursAhead is the coldest bucket's distance from now, in whole hours (3h-bucket granularity); MinTemperatureC/CloudinessPercent/WindSpeedMetersPerSecond are null only if that bucket's JSON omitted the field.
    public sealed record FrostForecastResult(double? MinTemperatureC, double? CloudinessPercent, double? WindSpeedMetersPerSecond, int HoursAhead);

    /// TemperatureC/HumidityPercent/WindSpeedMetersPerSecond are null only if the nearest bucket's JSON omitted that field.
    public sealed record OutdoorConditions(double? TemperatureC, double? HumidityPercent, double? WindSpeedMetersPerSecond);

    /// Uses OpenWeatherMap's free "5 day / 3 hour forecast" endpoint, not the paid One Call 3.0 - each bucket's "pop" (0-1) is a closer match to "will it rain soon" than the current-conditions endpoint.
    public sealed class OpenWeatherMapClient(HttpClient httpClient, ILogger<OpenWeatherMapClient> logger) : IWeatherForecastClient
    {
        private const string ForecastUrl = "https://api.openweathermap.org/data/2.5/forecast";

        // 8 buckets x 3h = next 24h - long enough to catch an approaching front, short enough that a "skip today's watering" decision still means something by the time it's acted on.
        private const int LookaheadBuckets = 8;

        public async Task<double?> GetMaxRainProbabilityPercentAsync(double lat, double lon, string apiKey, CancellationToken ct)
        {
            string url = $"{ForecastUrl}?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&appid={Uri.EscapeDataString(apiKey)}";
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("OpenWeatherMap forecast request failed with {Status}.", response.StatusCode);
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("list", out JsonElement list))
                {
                    return null;
                }

                double maxPop = 0;
                int seen = 0;
                foreach (JsonElement entry in list.EnumerateArray())
                {
                    if (seen++ >= LookaheadBuckets)
                    {
                        break;
                    }
                    if (entry.TryGetProperty("pop", out JsonElement popElement) && popElement.TryGetDouble(out double pop))
                    {
                        maxPop = Math.Max(maxPop, pop);
                    }
                }
                return maxPop * 100.0;
            }
            // Deliberately NOT catching OperationCanceledException/TaskCanceledException - a shutdown-triggered cancellation should propagate to PeriodicBackgroundService's own handler, not be logged as a fetch failure.
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                logger.LogWarning(ex, "OpenWeatherMap forecast fetch failed.");
                return null;
            }
        }

        public async Task<FrostForecastResult?> GetFrostForecastAsync(double lat, double lon, string apiKey, int lookaheadHours, CancellationToken ct)
        {
            // units=metric so "temp" comes back in Celsius directly - GetMaxRainProbabilityPercentAsync above never needed it since "pop" is unitless.
            string url = $"{ForecastUrl}?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&units=metric&appid={Uri.EscapeDataString(apiKey)}";
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("OpenWeatherMap forecast request failed with {Status}.", response.StatusCode);
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("list", out JsonElement list))
                {
                    return null;
                }

                int bucketsWanted = Math.Max(1, (int)Math.Ceiling(lookaheadHours / 3.0));
                FrostForecastResult? coldest = null;
                int seen = 0;
                foreach (JsonElement entry in list.EnumerateArray())
                {
                    if (seen >= bucketsWanted)
                    {
                        break;
                    }
                    seen++;

                    double? temp = entry.TryGetProperty("main", out JsonElement main) && main.TryGetProperty("temp", out JsonElement tempEl) && tempEl.TryGetDouble(out double t) ? t : null;
                    double? clouds = entry.TryGetProperty("clouds", out JsonElement cloudsEl) && cloudsEl.TryGetProperty("all", out JsonElement allEl) && allEl.TryGetDouble(out double c) ? c : null;
                    double? wind = entry.TryGetProperty("wind", out JsonElement windEl) && windEl.TryGetProperty("speed", out JsonElement speedEl) && speedEl.TryGetDouble(out double w) ? w : null;

                    if (temp is double tv && (coldest is null || coldest.MinTemperatureC is not double bestT || tv < bestT))
                    {
                        coldest = new FrostForecastResult(tv, clouds, wind, seen * 3);
                    }
                }
                return coldest;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                logger.LogWarning(ex, "OpenWeatherMap frost forecast fetch failed.");
                return null;
            }
        }

        public async Task<OutdoorConditions?> GetCurrentOutdoorConditionsAsync(double lat, double lon, string apiKey, CancellationToken ct)
        {
            string url = $"{ForecastUrl}?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&units=metric&appid={Uri.EscapeDataString(apiKey)}";
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("OpenWeatherMap forecast request failed with {Status}.", response.StatusCode);
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("list", out JsonElement list) || list.GetArrayLength() == 0)
                {
                    return null;
                }

                JsonElement nearest = list[0];
                double? temp = nearest.TryGetProperty("main", out JsonElement main) && main.TryGetProperty("temp", out JsonElement tempEl) && tempEl.TryGetDouble(out double t) ? t : null;
                double? humidity = nearest.TryGetProperty("main", out JsonElement main2) && main2.TryGetProperty("humidity", out JsonElement humEl) && humEl.TryGetDouble(out double h) ? h : null;
                double? wind = nearest.TryGetProperty("wind", out JsonElement windEl) && windEl.TryGetProperty("speed", out JsonElement speedEl) && speedEl.TryGetDouble(out double w) ? w : null;
                return new OutdoorConditions(temp, humidity, wind);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                logger.LogWarning(ex, "OpenWeatherMap current-conditions fetch failed.");
                return null;
            }
        }
    }
}
