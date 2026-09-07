using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Agrumy.Shared.Json;

namespace Agrumy.Shared.Models
{
    /// One item of the JSON array body for POST /api/SensorData - see contracts/device-api/sensordata.request.schema.json. DeviceID/TenantID/DeviceFarmUnitID/DeviceFarmUnitZoneID are present on the wire but always ignored (identity comes from the authenticated device, see SensorDataController.Post); every measurement accepts a JSON number OR a numeric string, since legacy pre-#326 firmware still sends strings.
    public class SensorDataPushReading
    {
        public int? DeviceID { get; set; }
        public int? TenantID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }

        [JsonConverter(typeof(LenientIntConverter))]
        public int? Battery { get; set; }
        [JsonConverter(typeof(LenientDoubleConverter))]
        public double? Temperature { get; set; }
        [JsonConverter(typeof(LenientDoubleConverter))]
        public double? SoilTemperature { get; set; }
        [JsonConverter(typeof(LenientDoubleConverter))]
        public double? Humidity { get; set; }
        [JsonConverter(typeof(LenientIntConverter))]
        public int? Moisture { get; set; }
        [JsonConverter(typeof(LenientIntConverter))]
        public int? Light { get; set; }
        [JsonConverter(typeof(LenientIntConverter))]
        public int? Co2 { get; set; }
        [JsonConverter(typeof(LenientIntConverter))]
        public int? Tvoc { get; set; }
        [JsonConverter(typeof(LenientDoubleConverter))]
        public double? Barometer { get; set; }
        [JsonConverter(typeof(LenientDoubleConverter))]
        public double? LiquidPH { get; set; }
        [JsonConverter(typeof(LenientIntConverter))]
        public int? RainLevel { get; set; }
        [JsonConverter(typeof(LenientIntConverter))]
        public int? WaterLevel { get; set; }
        [JsonConverter(typeof(LenientIntConverter))]
        public int? Wind { get; set; }
        [JsonConverter(typeof(LenientDoubleConverter))]
        public double? Ec { get; set; }
        [JsonConverter(typeof(LenientDoubleConverter))]
        public double? Weight { get; set; }
        // Device-side timestamp string (device.getDateTime()'s "yyyy-MM-dd HH:mm:ss", assumed UTC) - kept as a raw string here, parsed by EfSensorDataRepository the same way it always was.
        public string? DateCreated { get; set; }
    }

    public class SensorData
    {
        public int? TenantID { get; set; }
        public int? DeviceID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }

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
        public double? Wind { get; set; }
        public double? Ec { get; set; }
        public double? Weight { get; set; }
        public DateTimeOffset DateCreated { get; set; }


    }

    public enum TimeRangeMDMY
    {
        Minute = 0,
        Day = 1,
        Month = 2,
        Year = 3
    }

    public class TimeRange
    {
        // Must match SensorDataController's actual cap (one day of minute-resolution data).
        [Range(1, 1440)]
        public int? Range { get; set; } = 1;
    }


    public class SensorDataReport
    {
        public int? IDSensorDataReport { get; set; }
        public int? DeviceID { get; set; }
        public string? ReportName { get; set; }
        public DateTimeOffset? DateGenerated { get; set; }

        public string? SensorData { get; set; }

    }


}
