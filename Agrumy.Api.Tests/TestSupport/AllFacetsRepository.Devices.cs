using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IDeviceRepository core CRUD members - forwarded to the standalone EfDeviceRepository so RelationalIntegrationTests can drive many facets through one object. ToDto(DeviceRow) moved to EfDeviceRepository.ToDto - AllFacetsRepository.DeviceFarmUnits.cs (not yet extracted) now calls that directly.
    internal partial class AllFacetsRepository
    {
        public Task<Device> DeviceAddAsync(Device device, Func<Task<string?>>? quotaCheckAsync = null) => deviceRepository.DeviceAddAsync(device, quotaCheckAsync);

        public Task DeviceDeleteAsync(int? idDevice, int? tenantID) => deviceRepository.DeviceDeleteAsync(idDevice, tenantID);

        public Task<IList<Device>> DeviceRecycleBinGetAsync(int? tenantID) => deviceRepository.DeviceRecycleBinGetAsync(tenantID);

        public Task<Device?> DeviceRecycleBinGetByIdAsync(int idDevice) => deviceRepository.DeviceRecycleBinGetByIdAsync(idDevice);

        public Task<IList<Device>> DevicePendingPurgeGetAsync(int? tenantID) => deviceRepository.DevicePendingPurgeGetAsync(tenantID);

        public Task<bool> DeviceRestoreAsync(int idDevice, int? tenantID) => deviceRepository.DeviceRestoreAsync(idDevice, tenantID);

        public Task<bool> DeviceRecycleBinMarkPurgedAsync(int idDevice, int? tenantID) => deviceRepository.DeviceRecycleBinMarkPurgedAsync(idDevice, tenantID);

        public Task<int> DeviceRecycleBinMarkPurgedByRetentionAsync(int serverDefaultRetentionDays, CancellationToken ct) =>
            deviceRepository.DeviceRecycleBinMarkPurgedByRetentionAsync(serverDefaultRetentionDays, ct);

        public Task<IList<(int IDDevice, int? TenantID)>> DevicePurgedIdsGetAsync() => deviceRepository.DevicePurgedIdsGetAsync();

        public Task<bool> DeviceRecycleBinPurgeAsync(int idDevice, int? tenantID) => deviceRepository.DeviceRecycleBinPurgeAsync(idDevice, tenantID);

        public Task<Device?> DeviceGetAsync(int? tenantID, int? idDevice, string? apiId, string? macAddress) =>
            deviceRepository.DeviceGetAsync(tenantID, idDevice, apiId, macAddress);

        public Task<Device?> DeviceGetByIdAsync(int? idDevice) => deviceRepository.DeviceGetByIdAsync(idDevice);

        public Task<Device?> DeviceGetByApiIdAsync(string? apiId) => deviceRepository.DeviceGetByApiIdAsync(apiId);

        public Task<IList<Device>> DevicesGetAsync(int? tenantID) => deviceRepository.DevicesGetAsync(tenantID);

        public Task<IList<Device>> DevicesGetAllAsync() => deviceRepository.DevicesGetAllAsync();

        public Task<IList<Device>> DevicesSensorOnlyGetAsync(int? tenantID) => deviceRepository.DevicesSensorOnlyGetAsync(tenantID);

        public Task<bool> DeviceCheckMacAddressAsync(int? tenantID, string? macAddress) => deviceRepository.DeviceCheckMacAddressAsync(tenantID, macAddress);

        public Task DeviceUpdateAsync(Device? device) => deviceRepository.DeviceUpdateAsync(device);

        public Task DeviceMarkConfigSentAsync(int deviceID, DateTime sentAtUtc) => deviceRepository.DeviceMarkConfigSentAsync(deviceID, sentAtUtc);

        public Task DeviceSessionSetAsync(int deviceID, string? token, DateTimeOffset? expiresAtUtc) => deviceRepository.DeviceSessionSetAsync(deviceID, token, expiresAtUtc);

        public Task<(string Token, DateTimeOffset ExpiresAtUtc)?> DeviceSessionGetAsync(string apiId) => deviceRepository.DeviceSessionGetAsync(apiId);

        public Task DeviceSensorDetectionResultSetAsync(int deviceID, string? resultJson, DateTimeOffset detectedAt) => deviceRepository.DeviceSensorDetectionResultSetAsync(deviceID, resultJson, detectedAt);

        public Task<string> DeviceLoRaPrivateKeyGenerateAsync(int deviceID) => deviceRepository.DeviceLoRaPrivateKeyGenerateAsync(deviceID);

        public Task<bool> DeviceLoRaUplinkCounterSetAsync(int deviceID, long counter) => deviceRepository.DeviceLoRaUplinkCounterSetAsync(deviceID, counter);

        public Task<bool> DeviceLoRaSessionAcceptAsync(int deviceID, byte[] bootNonce, uint counter) => deviceRepository.DeviceLoRaSessionAcceptAsync(deviceID, bootNonce, counter);
    }
}
