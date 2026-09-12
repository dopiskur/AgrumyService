using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// OData/Power BI feed shape - exposes IDSensorData as the entity key, unlike Agrumy.Shared.Models.SensorData which no existing chart/export consumer needs it on.
    public sealed class SensorDataODataEntry
    {
        public int IDSensorData { get; set; }
        public int DeviceID { get; set; }
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
        public int? Wind { get; set; }
        public double? Ec { get; set; }
        public double? Weight { get; set; }
        public int? WifiRssiDbm { get; set; }
        public int? LoRaRssiDbm { get; set; }
        public int? LoRaSnrDb { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }

    /// Telemetry facet of the data layer.
    public interface ISensorDataRepository
    {
        /// Persists a telemetry batch - deviceID/tenantID/deviceFarmUnitID/deviceFarmUnitZoneID come from the authenticated identity and are applied to every row; matching fields on each reading itself are ignored.
        Task SensorDataPushAsync(IReadOnlyList<SensorDataPushReading> readings, int deviceID, int tenantID, int? deviceFarmUnitID, int? deviceFarmUnitZoneID);

        /// One row per bucket (latest raw reading in that bucket, not averaged) - aggregated in SQL (time_bucket/date_trunc on Postgres, DATE_FORMAT truncation on MySQL), never loads the raw row set into the app.
        Task<string> SensorDataGetAsync(int? tenantID, int? deviceID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket);

        /// Same JSON shape as SensorDataGetAsync, but time-bucket AVERAGED (plain SQL AVG(), no outlier trimming) across every device in the zone/unit instead of one device's own raw readings.
        Task<string> SensorDataZoneAverageGetAsync(int? tenantID, int deviceFarmUnitZoneID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket);
        Task<string> SensorDataUnitAverageGetAsync(int? tenantID, int deviceFarmUnitID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket);

        Task SensorDataDeleteAsync(int? tenantID, int? deviceID, DateTimeOffset olderThan);

        /// Raw, untransformed rows for a whole organization (organization export), not shaped for chart consumption like SensorDataGetAsync - sinceUtc null means every row ever recorded.
        Task<IList<SensorData>> SensorDataExportGetAsync(int tenantID, DateTime? sinceUtc);

        /// Bulk-inserts already-remapped rows (organization import) - the caller has resolved every id to its new value on the target server, this just persists them as-is.
        Task SensorDataImportAsync(IList<SensorData> rows);

        /// Downsamples every row older than cutoffUtc, per device, into one 5-minute-bucket average-without-outliers row, replacing the raw rows in place.
        Task OptimizeOldSensorDataAsync(DateTime cutoffUtc, CancellationToken ct);

        /// Deletes rows older than cutoffUtc outright (drop_chunks() on TimescaleDB, plain DELETE in PurgeBatchSize-row chunks on MariaDB/MySQL - batching alone is enough, no separate locking OPTIMIZE TABLE rebuild).
        Task PurgeOldSensorDataAsync(DateTime cutoffUtc, CancellationToken ct);

        /// Organization-scoped IQueryable for the OData/Power BI feed - filtered to tenantID here, server-side, before OData's [EnableQuery] layers $filter/$select/$orderby/$top on top; the client's OData query can never widen this to another organization's rows.
        IQueryable<SensorDataODataEntry> SensorDataODataQueryable(int tenantID);
    }
}
