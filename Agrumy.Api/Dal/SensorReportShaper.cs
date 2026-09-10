using System.Text.Json;
using Agrumy.Shared.Utils;

namespace Agrumy.Api.Dal
{
    /// One row per bucket, already aggregated in SQL (see EfSensorDataRepository) - JSON assembly only, no grouping left to do here.
    internal static class SensorReportShaper
    {
        /// <returns>JSON {"sensorData":[...]}, or "" when no buckets matched.</returns>
        public static string Build(IReadOnlyList<BucketedSensorRow> buckets)
        {
            if (buckets.Count == 0)
            {
                return "";
            }

            var records = buckets.Select(r => new Dictionary<string, object?>
            {
                ["battery"] = r.Battery,
                ["temperature"] = r.Temperature,
                ["soilTemperature"] = r.SoilTemperature,
                ["humidity"] = r.Humidity,
                ["vpd"] = VpdCalculator.Compute(r.Temperature, r.Humidity),
                ["moisture"] = r.Moisture,
                ["light"] = r.Light,
                ["co2"] = r.Co2,
                ["tvoc"] = r.Tvoc,
                ["barometer"] = r.Barometer,
                ["liquidPH"] = r.LiquidPH,
                ["rainLevel"] = r.RainLevel,
                ["waterLevel"] = r.WaterLevel,
                ["wind"] = r.Wind,
                ["ec"] = r.Ec,
                ["weight"] = r.Weight,
                ["dateCreated"] = r.BucketStart.ToString("yyyy-MM-dd HH:mm:ss"),
            });

            return JsonSerializer.Serialize(new Dictionary<string, object?> { ["sensorData"] = records });
        }

        /// Same shape as Build, but every column is already a SQL AVG() (a zone/unit spans several devices) - VPD is computed from the bucket's averaged temperature/humidity rather than averaged per-row, a deliberate approximation (Jensen's-inequality-scale, negligible at chart resolution) now that the per-row values never reach the app.
        public static string BuildAveraged(IReadOnlyList<AveragedSensorBucket> buckets)
        {
            if (buckets.Count == 0)
            {
                return "";
            }

            var records = buckets.Select(r => new Dictionary<string, object?>
            {
                ["battery"] = r.Battery,
                ["temperature"] = r.Temperature,
                ["soilTemperature"] = r.SoilTemperature,
                ["humidity"] = r.Humidity,
                ["vpd"] = VpdCalculator.Compute(r.Temperature, r.Humidity),
                ["moisture"] = r.Moisture,
                ["light"] = r.Light,
                ["co2"] = r.Co2,
                ["tvoc"] = r.Tvoc,
                ["barometer"] = r.Barometer,
                ["liquidPH"] = r.LiquidPH,
                ["rainLevel"] = r.RainLevel,
                ["waterLevel"] = r.WaterLevel,
                ["wind"] = r.Wind,
                ["ec"] = r.Ec,
                ["weight"] = r.Weight,
                ["dateCreated"] = r.BucketStart.ToString("yyyy-MM-dd HH:mm:ss"),
            });

            return JsonSerializer.Serialize(new Dictionary<string, object?> { ["sensorData"] = records });
        }
    }

    /// One bucket's latest raw reading (not averaged) - SensorDataGetAsync's shape; int columns stay int since no averaging happens.
    internal sealed class BucketedSensorRow
    {
        public int? Battery { get; set; }
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        public int? Moisture { get; set; }
        public int? Light { get; set; }
        public int? Co2 { get; set; }
        public int? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public int? RainLevel { get; set; }
        public int? WaterLevel { get; set; }
        public int? Wind { get; set; }
        public double? Ec { get; set; }
        public double? Weight { get; set; }
        public DateTime BucketStart { get; set; }
    }

    /// One bucket's SQL AVG() across every contributing device - SensorDataZoneAverageGetAsync/SensorDataUnitAverageGetAsync's shape; every column is a double since AVG() always returns a fractional type, matching the old C# Enumerable.Average() behavior this replaces.
    internal sealed class AveragedSensorBucket
    {
        public double? Battery { get; set; }
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        public double? Moisture { get; set; }
        public double? Light { get; set; }
        public double? Co2 { get; set; }
        public double? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public double? RainLevel { get; set; }
        public double? WaterLevel { get; set; }
        public double? Wind { get; set; }
        public double? Ec { get; set; }
        public double? Weight { get; set; }
        public DateTime BucketStart { get; set; }
    }
}
