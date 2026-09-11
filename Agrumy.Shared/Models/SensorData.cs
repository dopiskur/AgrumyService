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
        // WiFi RSSI at push time (device's own WiFi.RSSI()), null for a LoRa-relayed reading with no WiFi radio.
        [JsonConverter(typeof(LenientIntConverter))]
        public int? WifiRssiDbm { get; set; }
        // Never set by the node itself (a LoRa transmitter can't know its own reception quality); GatewayApiController.RunSensorDataAsync fills these in from the relaying gateway's own radio measurement.
        public int? LoRaRssiDbm { get; set; }
        public int? LoRaSnrDb { get; set; }
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
        public int? WifiRssiDbm { get; set; }
        public int? LoRaRssiDbm { get; set; }
        public int? LoRaSnrDb { get; set; }
        public DateTimeOffset DateCreated { get; set; }


    }

    /// One day's average Moisture for a FarmParcelZone - pairs with SatelliteSeriesPoint on the Zone-tab dual-axis trend chart (#558).
    public class FarmParcelZoneMoistureSeriesPoint
    {
        public DateOnly Date { get; set; }
        public double? Moisture { get; set; }
    }

    /// The only bucket granularities SensorReportShaper's SQL ever groups by - the from/to window and this together fully replace the old timeRange+timeMDMY encoding.
    public enum SensorDataBucket
    {
        Minute = 0,
        Hour = 1,
        Day = 2,
    }
}
