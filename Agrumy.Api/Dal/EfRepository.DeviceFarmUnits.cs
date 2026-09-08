using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal
{
    /// IDeviceFarmUnitRepository members - forwarded to the standalone EfDeviceFarmUnitRepository (roadmap #246) so IRepository's broad consumers keep working unchanged.
    internal partial class EfRepository
    {
        public Task<IList<DeviceFarm>> DeviceFarmsGetAsync(int? tenantID) => deviceFarmUnitRepository.DeviceFarmsGetAsync(tenantID);

        public Task<DeviceFarm?> DeviceFarmGetByIdAsync(int? idDeviceFarm) => deviceFarmUnitRepository.DeviceFarmGetByIdAsync(idDeviceFarm);

        public Task<DeviceFarm> DeviceFarmAddAsync(DeviceFarm farm) => deviceFarmUnitRepository.DeviceFarmAddAsync(farm);

        public Task EnsureFirstFarmAsync(int tenantId) => deviceFarmUnitRepository.EnsureFirstFarmAsync(tenantId);

        public Task DeviceFarmUpdateAsync(DeviceFarm farm) => deviceFarmUnitRepository.DeviceFarmUpdateAsync(farm);

        public Task DeviceFarmDeleteAsync(int idDeviceFarm) => deviceFarmUnitRepository.DeviceFarmDeleteAsync(idDeviceFarm);

        public Task<IList<DeviceFarm>> DeviceFarmRecycleBinGetAsync(int? tenantID) => deviceFarmUnitRepository.DeviceFarmRecycleBinGetAsync(tenantID);

        public Task<DeviceFarm?> DeviceFarmRecycleBinGetByIdAsync(int idDeviceFarm) => deviceFarmUnitRepository.DeviceFarmRecycleBinGetByIdAsync(idDeviceFarm);

        public Task<IList<DeviceFarm>> DeviceFarmPendingPurgeGetAsync(int? tenantID) => deviceFarmUnitRepository.DeviceFarmPendingPurgeGetAsync(tenantID);

        public Task<bool> DeviceFarmRestoreAsync(int idDeviceFarm, int? tenantID) => deviceFarmUnitRepository.DeviceFarmRestoreAsync(idDeviceFarm, tenantID);

        public Task<bool> DeviceFarmRecycleBinMarkPurgedAsync(int idDeviceFarm, int? tenantID) => deviceFarmUnitRepository.DeviceFarmRecycleBinMarkPurgedAsync(idDeviceFarm, tenantID);

        public Task<int> DeviceFarmRecycleBinMarkPurgedByRetentionAsync(int serverDefaultRetentionDays, CancellationToken ct) =>
            deviceFarmUnitRepository.DeviceFarmRecycleBinMarkPurgedByRetentionAsync(serverDefaultRetentionDays, ct);

        public Task<IList<(int IDDeviceFarm, int? TenantID)>> DeviceFarmPurgedIdsGetAsync() => deviceFarmUnitRepository.DeviceFarmPurgedIdsGetAsync();

        public Task<bool> DeviceFarmRecycleBinPurgeAsync(int idDeviceFarm, int? tenantID) => deviceFarmUnitRepository.DeviceFarmRecycleBinPurgeAsync(idDeviceFarm, tenantID);

        public Task<IList<DeviceFarmUnit>> DeviceFarmUnitsGetAsync(int? tenantID) => deviceFarmUnitRepository.DeviceFarmUnitsGetAsync(tenantID);

        public Task<DeviceFarmUnit?> DeviceFarmUnitGetByIdAsync(int? idDeviceFarmUnit) => deviceFarmUnitRepository.DeviceFarmUnitGetByIdAsync(idDeviceFarmUnit);

        public Task<DeviceFarmUnit> DeviceFarmUnitAddAsync(DeviceFarmUnit unit) => deviceFarmUnitRepository.DeviceFarmUnitAddAsync(unit);

        public Task DeviceFarmUnitUpdateAsync(DeviceFarmUnit unit) => deviceFarmUnitRepository.DeviceFarmUnitUpdateAsync(unit);

        public Task DeviceFarmUnitDeleteAsync(int idDeviceFarmUnit) => deviceFarmUnitRepository.DeviceFarmUnitDeleteAsync(idDeviceFarmUnit);

        public Task<IList<DeviceFarmUnitZone>> DeviceFarmUnitZonesGetAsync(int idDeviceFarmUnit) => deviceFarmUnitRepository.DeviceFarmUnitZonesGetAsync(idDeviceFarmUnit);

        public Task<DeviceFarmUnitZone?> DeviceFarmUnitZoneGetByIdAsync(int? idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneGetByIdAsync(idDeviceFarmUnitZone);

        public Task<DeviceFarmUnitZone> DeviceFarmUnitZoneAddAsync(DeviceFarmUnitZone zone) => deviceFarmUnitRepository.DeviceFarmUnitZoneAddAsync(zone);

        public Task DeviceFarmUnitZoneUpdateAsync(DeviceFarmUnitZone zone) => deviceFarmUnitRepository.DeviceFarmUnitZoneUpdateAsync(zone);

        public Task DeviceFarmUnitZoneWidgetsSetAsync(int idDeviceFarmUnitZone, List<DashboardWidget> widgets) => deviceFarmUnitRepository.DeviceFarmUnitZoneWidgetsSetAsync(idDeviceFarmUnitZone, widgets);

        public Task DeviceFarmUnitZoneConfigVersionBumpAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneConfigVersionBumpAsync(idDeviceFarmUnitZone);

        public Task DeviceFarmUnitZoneDeleteAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneDeleteAsync(idDeviceFarmUnitZone);

        public Task<IList<DeviceFarmUnitZoneRule>> RulesGetForZoneAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.RulesGetForZoneAsync(idDeviceFarmUnitZone);

        public Task<IList<DeviceFarmUnitZoneRule>> RulesGetForUnitAsync(int idDeviceFarmUnit) => deviceFarmUnitRepository.RulesGetForUnitAsync(idDeviceFarmUnit);

        public Task<IList<DeviceFarmUnitZoneRule>> RulesGetForFarmAsync(int idDeviceFarm) => deviceFarmUnitRepository.RulesGetForFarmAsync(idDeviceFarm);

        public Task<IList<DeviceFarmUnitZoneRule>> RulesGetForTenantGlobalAsync(int tenantId) => deviceFarmUnitRepository.RulesGetForTenantGlobalAsync(tenantId);

        public Task<IList<DeviceFarmUnitZoneRule>> RulesGetNotificationRulesForTenantAsync(int tenantId) => deviceFarmUnitRepository.RulesGetNotificationRulesForTenantAsync(tenantId);

        public Task<DeviceFarmUnitZoneRule?> RuleGetByIdAsync(int? idRule) => deviceFarmUnitRepository.RuleGetByIdAsync(idRule);

        public Task<int> RuleAddAsync(DeviceFarmUnitZoneRule rule) => deviceFarmUnitRepository.RuleAddAsync(rule);

        public Task<IList<DeviceFarmUnitZoneRule>> RulesReferencingAsync(int ruleId, int tenantId) => deviceFarmUnitRepository.RulesReferencingAsync(ruleId, tenantId);

        public Task RuleDeleteAsync(int idRule) => deviceFarmUnitRepository.RuleDeleteAsync(idRule);

        public Task<bool> RuleNotificationWasTrueGetAsync(int ruleId, int idDeviceFarmUnitZone) => deviceFarmUnitRepository.RuleNotificationWasTrueGetAsync(ruleId, idDeviceFarmUnitZone);

        public Task RuleNotificationWasTrueSetAsync(int ruleId, int idDeviceFarmUnitZone, bool wasTrue, DateTime? lastFiredAtUtc) =>
            deviceFarmUnitRepository.RuleNotificationWasTrueSetAsync(ruleId, idDeviceFarmUnitZone, wasTrue, lastFiredAtUtc);

        public Task<bool> DeviceFarmUnitZoneHasControllerAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneHasControllerAsync(idDeviceFarmUnitZone);

        public Task<Device?> DeviceFarmUnitZoneGetControllerAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneGetControllerAsync(idDeviceFarmUnitZone);

        public Task<IList<Device>> DeviceFarmUnitGetControllersAsync(int idDeviceFarmUnit) => deviceFarmUnitRepository.DeviceFarmUnitGetControllersAsync(idDeviceFarmUnit);

        public Task<IList<Device>> DeviceFarmUnitZoneGetSensorsAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneGetSensorsAsync(idDeviceFarmUnitZone);

        public Task<IList<Device>> DeviceFarmUnitGetSensorsAsync(int idDeviceFarmUnit) => deviceFarmUnitRepository.DeviceFarmUnitGetSensorsAsync(idDeviceFarmUnit);

        public Task<IList<Device>> DeviceFarmUnitGetDevicesAsync(int idDeviceFarmUnit) => deviceFarmUnitRepository.DeviceFarmUnitGetDevicesAsync(idDeviceFarmUnit);

        public Task<IList<Device>> DeviceUnassignedGetAsync(int? tenantID, bool controllerCapable) => deviceFarmUnitRepository.DeviceUnassignedGetAsync(tenantID, controllerCapable);

        public Task DeviceAssignToZoneAsync(int idDevice, int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceAssignToZoneAsync(idDevice, idDeviceFarmUnitZone);

        public Task DeviceUnassignFromZoneAsync(int idDevice) => deviceFarmUnitRepository.DeviceUnassignFromZoneAsync(idDevice);

        public Task<IList<DeviceFarmUnitDashboard>> DeviceFarmUnitDashboardGetAsync(int? tenantID) => deviceFarmUnitRepository.DeviceFarmUnitDashboardGetAsync(tenantID);

        public Task<IList<DeviceFarmUnitZoneDashboard>> DeviceFarmUnitZoneDashboardListGetAsync(int idDeviceFarmUnit) => deviceFarmUnitRepository.DeviceFarmUnitZoneDashboardListGetAsync(idDeviceFarmUnit);

        public Task<DeviceFarmUnitZoneDashboard?> DeviceFarmUnitZoneDashboardGetAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneDashboardGetAsync(idDeviceFarmUnitZone);

        public Task<DeviceFarmUnitZoneDashboard?> DeviceFarmUnitZoneDashboardForDisplayGetAsync(int idDeviceFarmUnitZone) => deviceFarmUnitRepository.DeviceFarmUnitZoneDashboardForDisplayGetAsync(idDeviceFarmUnitZone);

        public Task<IList<TankRefillAlertCandidate>> TankRefillAlertCandidatesGetAsync() => deviceFarmUnitRepository.TankRefillAlertCandidatesGetAsync();

        public Task TankRefillNotifiedSetAsync(int idDeviceFarmUnitZone, DateTimeOffset? notifiedAt) => deviceFarmUnitRepository.TankRefillNotifiedSetAsync(idDeviceFarmUnitZone, notifiedAt);

        public Task ManualOverrideStartAsync(DeviceManualOverride manualOverride) => deviceFarmUnitRepository.ManualOverrideStartAsync(manualOverride);

        public Task ManualOverrideStopAsync(int deviceId, RelayFunction relayFunction) => deviceFarmUnitRepository.ManualOverrideStopAsync(deviceId, relayFunction);

        public Task<IList<DeviceManualOverride>> ManualOverridesActiveForDeviceAsync(int deviceId) => deviceFarmUnitRepository.ManualOverridesActiveForDeviceAsync(deviceId);
    }
}
