using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal
{
    /// IDeviceRepository diagnostics/fleet/events/alert members - forwarded to the standalone EfDeviceRepository (roadmap #246) so IRepository's broad consumers keep working unchanged.
    internal partial class EfRepository
    {
        public Task DeviceDiagnosticUpsertAsync(int deviceID, int tenantID, DeviceConfigPoll poll) => deviceRepository.DeviceDiagnosticUpsertAsync(deviceID, tenantID, poll);

        public Task<bool> DeviceCheckAndRecordSensorPushAsync(int deviceID, TimeSpan minInterval) => deviceRepository.DeviceCheckAndRecordSensorPushAsync(deviceID, minInterval);

        public Task<IList<DeviceFleetStatus>> DeviceFleetGetAsync(int? tenantID) => deviceRepository.DeviceFleetGetAsync(tenantID);

        public Task InvalidateFleetCacheAsync(int? tenantID) => deviceRepository.InvalidateFleetCacheAsync(tenantID);

        public Task<DeviceFleetStatus?> DeviceFleetStatusGetAsync(int deviceID, int? tenantID) => deviceRepository.DeviceFleetStatusGetAsync(deviceID, tenantID);

        public Task<bool> EventDevicePushAsync(int deviceID, int tenantID, DeviceEventType eventType, string? message) =>
            deviceRepository.EventDevicePushAsync(deviceID, tenantID, eventType, message);

        public Task<IList<DeviceEvent>> EventDeviceGetAsync(int? deviceID, int? tenantID, int limit = 100) => deviceRepository.EventDeviceGetAsync(deviceID, tenantID, limit);

        public Task<bool> EventDeviceAcknowledgeAsync(int idEventDevice, int? tenantID) => deviceRepository.EventDeviceAcknowledgeAsync(idEventDevice, tenantID);

        public Task<IList<OfflineAlertCandidate>> OfflineAlertCandidatesGetAsync() => deviceRepository.OfflineAlertCandidatesGetAsync();

        public Task DeviceOfflineNotifiedSetAsync(int deviceID, DateTimeOffset? notifiedAt) => deviceRepository.DeviceOfflineNotifiedSetAsync(deviceID, notifiedAt);

        public Task<IList<LowBatteryAlertCandidate>> LowBatteryAlertCandidatesGetAsync() => deviceRepository.LowBatteryAlertCandidatesGetAsync();

        public Task DeviceLowBatteryNotifiedSetAsync(int deviceID, DateTimeOffset? notifiedAt) => deviceRepository.DeviceLowBatteryNotifiedSetAsync(deviceID, notifiedAt);
    }
}
