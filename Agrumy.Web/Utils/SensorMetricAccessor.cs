using Agrumy.Shared.Models;

namespace Agrumy.Web.Utils
{
    /// Roadmap #238 - maps a SensorMetric enum value to the matching field on SensorAverages/SensorTrend, so a widget's stored Metric selection can pull the right reading without a giant switch at every call site.
    public static class SensorMetricAccessor
    {
        public static string Label(SensorMetric metric) => metric switch
        {
            SensorMetric.Temperature => "Temperature",
            SensorMetric.SoilTemperature => "Soil temperature",
            SensorMetric.Humidity => "Humidity",
            SensorMetric.Vpd => "VPD",
            SensorMetric.Moisture => "Moisture",
            SensorMetric.Light => "Light",
            SensorMetric.Co2 => "CO2",
            SensorMetric.Tvoc => "TVOC",
            SensorMetric.Barometer => "Barometer",
            SensorMetric.LiquidPH => "pH",
            SensorMetric.RainLevel => "Rain level",
            SensorMetric.WaterLevel => "Water level",
            SensorMetric.Wind => "Wind",
            SensorMetric.DewPoint => "Dew point",
            SensorMetric.DewPointSpread => "Dew point spread",
            SensorMetric.Ec => "EC",
            SensorMetric.Weight => "Weight",
            _ => metric.ToString(),
        };

        public static double? Average(SensorAverages a, SensorMetric metric) => metric switch
        {
            SensorMetric.Temperature => a.Temperature,
            SensorMetric.SoilTemperature => a.SoilTemperature,
            SensorMetric.Humidity => a.Humidity,
            SensorMetric.Vpd => a.Vpd,
            SensorMetric.Moisture => a.Moisture,
            SensorMetric.Light => a.Light,
            SensorMetric.Co2 => a.Co2,
            SensorMetric.Tvoc => a.Tvoc,
            SensorMetric.Barometer => a.Barometer,
            SensorMetric.LiquidPH => a.LiquidPH,
            SensorMetric.RainLevel => a.RainLevel,
            SensorMetric.WaterLevel => a.WaterLevel,
            SensorMetric.Wind => a.Wind,
            SensorMetric.DewPoint => a.DewPoint,
            SensorMetric.DewPointSpread => a.DewPointSpread,
            SensorMetric.Ec => a.Ec,
            SensorMetric.Weight => a.Weight,
            _ => null,
        };

        public static double?[] Trend(SensorTrend t, SensorMetric metric) => metric switch
        {
            SensorMetric.Temperature => t.Temperature,
            SensorMetric.SoilTemperature => t.SoilTemperature,
            SensorMetric.Humidity => t.Humidity,
            SensorMetric.Vpd => t.Vpd,
            SensorMetric.Moisture => t.Moisture,
            SensorMetric.Light => t.Light,
            SensorMetric.Co2 => t.Co2,
            SensorMetric.Tvoc => t.Tvoc,
            SensorMetric.Barometer => t.Barometer,
            SensorMetric.LiquidPH => t.LiquidPH,
            SensorMetric.RainLevel => t.RainLevel,
            SensorMetric.WaterLevel => t.WaterLevel,
            SensorMetric.Wind => t.Wind,
            SensorMetric.DewPoint => t.DewPoint,
            SensorMetric.DewPointSpread => t.DewPointSpread,
            SensorMetric.Ec => t.Ec,
            SensorMetric.Weight => t.Weight,
            _ => new double?[SensorTrend.HourBuckets],
        };
    }
}
