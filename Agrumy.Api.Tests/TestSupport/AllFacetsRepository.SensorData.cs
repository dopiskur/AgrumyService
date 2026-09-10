using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// ISensorDataRepository members - forwarded to the standalone EfSensorDataRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task SensorDataPushAsync(IReadOnlyList<SensorDataPushReading> readings, int deviceID, int tenantID, int? deviceFarmUnitID, int? deviceFarmUnitZoneID) =>
            sensorDataRepository.SensorDataPushAsync(readings, deviceID, tenantID, deviceFarmUnitID, deviceFarmUnitZoneID);

        public Task<string> SensorDataGetAsync(int? tenantID, int? deviceID, int? timeRange, int? timeMDMY, int? buildReport) =>
            sensorDataRepository.SensorDataGetAsync(tenantID, deviceID, timeRange, timeMDMY, buildReport);

        public Task<string> SensorDataZoneAverageGetAsync(int? tenantID, int deviceFarmUnitZoneID, int? timeRange, int? timeMDMY) =>
            sensorDataRepository.SensorDataZoneAverageGetAsync(tenantID, deviceFarmUnitZoneID, timeRange, timeMDMY);

        public Task<string> SensorDataUnitAverageGetAsync(int? tenantID, int deviceFarmUnitID, int? timeRange, int? timeMDMY) =>
            sensorDataRepository.SensorDataUnitAverageGetAsync(tenantID, deviceFarmUnitID, timeRange, timeMDMY);

        public Task<IList<SensorDataReport>> SensorDataReportGetAsync(int? tenantID, int? getData, int? deviceID, int? sensorDataReportID) =>
            sensorDataRepository.SensorDataReportGetAsync(tenantID, getData, deviceID, sensorDataReportID);

        public Task SensorDataDeleteAsync(int? tenantID, int? deviceID, int? timeRange, int? timeMDMY) =>
            sensorDataRepository.SensorDataDeleteAsync(tenantID, deviceID, timeRange, timeMDMY);

        public Task<IList<SensorData>> SensorDataExportGetAsync(int tenantID, DateTime? sinceUtc) => sensorDataRepository.SensorDataExportGetAsync(tenantID, sinceUtc);

        public Task SensorDataImportAsync(IList<SensorData> rows) => sensorDataRepository.SensorDataImportAsync(rows);

        public Task OptimizeOldSensorDataAsync(DateTime cutoffUtc, CancellationToken ct) => sensorDataRepository.OptimizeOldSensorDataAsync(cutoffUtc, ct);

        public Task PurgeOldSensorDataAsync(DateTime cutoffUtc, bool shrinkAfterPurge, CancellationToken ct) => sensorDataRepository.PurgeOldSensorDataAsync(cutoffUtc, shrinkAfterPurge, ct);
    }
}
