using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// ISensorDataRepository members - forwarded to the standalone EfSensorDataRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task SensorDataPushAsync(IReadOnlyList<SensorDataPushReading> readings, int deviceID, int tenantID, int? deviceFarmUnitID, int? deviceFarmUnitZoneID) =>
            sensorDataRepository.SensorDataPushAsync(readings, deviceID, tenantID, deviceFarmUnitID, deviceFarmUnitZoneID);

        public Task<string> SensorDataGetAsync(int? tenantID, int? deviceID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket) =>
            sensorDataRepository.SensorDataGetAsync(tenantID, deviceID, from, to, bucket);

        public Task<string> SensorDataZoneAverageGetAsync(int? tenantID, int deviceFarmUnitZoneID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket) =>
            sensorDataRepository.SensorDataZoneAverageGetAsync(tenantID, deviceFarmUnitZoneID, from, to, bucket);

        public Task<string> SensorDataUnitAverageGetAsync(int? tenantID, int deviceFarmUnitID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket) =>
            sensorDataRepository.SensorDataUnitAverageGetAsync(tenantID, deviceFarmUnitID, from, to, bucket);

        public Task SensorDataDeleteAsync(int? tenantID, int? deviceID, DateTimeOffset olderThan) =>
            sensorDataRepository.SensorDataDeleteAsync(tenantID, deviceID, olderThan);

        public Task<IList<SensorData>> SensorDataExportGetAsync(int tenantID, DateTime? sinceUtc) => sensorDataRepository.SensorDataExportGetAsync(tenantID, sinceUtc);

        public Task SensorDataImportAsync(IList<SensorData> rows) => sensorDataRepository.SensorDataImportAsync(rows);

        public Task OptimizeOldSensorDataAsync(DateTime cutoffUtc, CancellationToken ct) => sensorDataRepository.OptimizeOldSensorDataAsync(cutoffUtc, ct);

        public Task PurgeOldSensorDataAsync(DateTime cutoffUtc, bool shrinkAfterPurge, CancellationToken ct) => sensorDataRepository.PurgeOldSensorDataAsync(cutoffUtc, shrinkAfterPurge, ct);

        public IQueryable<SensorDataODataEntry> SensorDataODataQueryable(int tenantID) => sensorDataRepository.SensorDataODataQueryable(tenantID);
    }
}
