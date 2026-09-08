using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;

namespace Agrumy.Api.Devices
{
    /// Builds the DeviceConfig body a Config poll or Register response sends back, shared so GatewayApiController.Batch's Config entries produce byte-for-byte the same response as a direct POST /api/Device/Config.
    public class DeviceConfigBuilder(IRepository repo, FirmwareCatalogService firmwareCatalog)
    {
        /// Whether GetConfig/RunConfigAsync must send a full config this poll: a real version mismatch, a pending command, or - because BuildAsync recomputes UtcOffsetSeconds/SkipWaterPumpForRain fresh every call without either ever bumping ConfigVersion - the periodic heartbeat window has elapsed since the device's last full send. Not used by Register, which always sends a fresh config unconditionally.
        public async Task<bool> NeedsRefreshAsync(Device device, int? pollConfigVersion, PendingCommand? pendingCommand)
        {
            if (pollConfigVersion != device.ConfigVersion || pendingCommand != null)
            {
                return true;
            }
            ServerConfig serverConfig = await repo.ServerConfigGetAsync(1);
            if (serverConfig.ConfigHeartbeatHours <= 0)
            {
                return false;
            }
            return device.LastFullConfigSentAt is not DateTimeOffset last
                || (DateTimeOffset.UtcNow - last).TotalHours >= serverConfig.ConfigHeartbeatHours;
        }

        public async Task<DeviceConfig> BuildAsync(Device device, PendingCommand? pendingCommand, string? board)
        {
            // Computed fresh (not cached) every response so a DST shift or ScheduleTimeZone change reaches every device on its next poll; also reused below for WeatherRainPredicted.
            ServerConfig serverConfig = await repo.ServerConfigGetAsync(1);
            // Per-tenant, not global - a device with no tenant (roadmap #406, genuinely unassigned) or an unset zone both fall back to UTC via GetUtcOffsetSeconds' own null handling.
            Tenant? tenant = device.TenantID is int tenantId ? await repo.TenantGetByIdAsync(tenantId) : null;
            int utcOffsetSeconds = TimeZoneHelper.GetUtcOffsetSeconds(DateTime.UtcNow, tenant?.ScheduleTimeZone);

            var deviceConfig = new DeviceConfig
            {
                ConfigVersion = device.ConfigVersion,
                TenantID = device.TenantID,
                deviceID = device.IDDevice,
                DeviceFarmUnitID = device.DeviceFarmUnitID,
                DeviceFarmUnitZoneID = device.DeviceFarmUnitZoneID,
                ApiId = device.ApiId,
                ApiKey = device.ApiKey,
                ServicePoint = device.ServicePoint,
                DeviceTypeServiceID = device.DeviceTypeServiceID,
                ServicePublicKey = device.ServicePublicKey,
                SleepSeconds = device.SleepSeconds,
                SleepDeep = device.SleepDeepEnabled,
                LoRaGatewayEnabled = device.LoRaGatewayEnabled,
                UtcOffsetSeconds = utcOffsetSeconds,
                ServerUtcEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                DeviceSensorEnabled = device.DeviceSensorEnabled,
                DeviceControllerEnabled = device.DeviceControllerEnabled,
                BatteryEnabled = device.BatteryEnabled,
                Debug = device.Debug,
                Reboot = device.Reboot,
                Reset = device.Reset,
                FirmwareUpdate = device.FirmwareUpdate,
                Enabled = device.Enabled,
                EmergencyStop = tenant?.EmergencyStopActive == true,
                CommandVersion = device.CommandVersion,
                PendingCommand = pendingCommand,
                SimulationModeEnabled = (await repo.DeviceSimulationGetAsync(device.IDDevice!.Value))?.Enabled == true,
            };

            // Fire-once, cleared the instant it's included rather than waiting for a confirmation that can never come back - a device told to reset() wipes itself and restarts before it could ever report anything, so "wait for the device to confirm" (FirmwareUpdate's pattern) would leave this stuck true and re-trigger on every future poll after the device re-registers.
            if (device.Reset == true)
            {
                await repo.DeviceHardResetSetAsync(device.IDDevice!.Value, false);
            }

            // Firmware compares versions itself, so an offer present on every Config sync is fine, and harmless on Register too since ResolveOfferAsync returns null for a freshly-created device.
            DeviceFirmware? firmware = await firmwareCatalog.ResolveOfferAsync(device, board);
            if (firmware != null)
            {
                deviceConfig.FirmwareVersion = firmware.Version;
                deviceConfig.FirmwareUrl = firmware.Url;
                deviceConfig.FirmwareSha256 = firmware.Sha256;
            }

            if (deviceConfig.DeviceSensorEnabled == true)
            {
                deviceConfig.DeviceConfigSensor = await repo.DeviceConfigSensorGetAsync(device.DeviceConfigSensorID);
            }
            if (deviceConfig.DeviceControllerEnabled == true)
            {
                // Relay-pin mapping comes from the device row, but Rules/safety limits come from its zone, merged into the same DeviceConfigController; no zone means an empty Rules list so every relay stays off.
                DeviceConfigController? controller = await repo.DeviceConfigControllerGetAsync(device.DeviceConfigControllerID);
                if (controller != null && device.DeviceFarmUnitZoneID is int idZone)
                {
                    // Most specific tier, checked ahead of the real hierarchy below - empty unless this device is currently a member of an active simulation session, in which case that session's own rules apply first, falling back to the real hierarchy for whatever they don't cover.
                    IList<DeviceFarmUnitZoneRule> simulationRules = await repo.DeviceActiveSimulationSessionIdGetAsync(device.IDDevice!.Value) is int idSession
                        ? await repo.RulesGetForSimulationAsync(idSession) : [];
                    // One tier below Simulation; empty unless the zone is currently under an active Experiment (Zone>Unit>Farm cascade resolved by ActiveExperimentIdForZoneAsync itself).
                    IList<DeviceFarmUnitZoneRule> experimentRules = await repo.ActiveExperimentIdForZoneAsync(idZone) is int idExperiment
                        ? await repo.RulesGetForExperimentAsync(idExperiment) : [];
                    IList<DeviceFarmUnitZoneRule> zoneRules = await repo.RulesGetForZoneAsync(idZone);
                    IList<DeviceFarmUnitZoneRule> unitRules = device.DeviceFarmUnitID is int idUnit ? await repo.RulesGetForUnitAsync(idUnit) : [];
                    // Farm rules only apply when the device's own Unit is actually assigned to one - a Farm-less Unit sees no Farm-scope rules at all, same "unassigned means no inheritance" rule as Global always applying regardless.
                    IList<DeviceFarmUnitZoneRule> farmRules = device.DeviceFarmUnitID is int farmUnitId
                        && (await repo.DeviceFarmUnitGetByIdAsync(farmUnitId))?.DeviceFarmID is int idFarm
                        ? await repo.RulesGetForFarmAsync(idFarm) : [];
                    IList<DeviceFarmUnitZoneRule> globalRules = device.TenantID is int globalTenantId ? await repo.RulesGetForTenantGlobalAsync(globalTenantId) : [];
                    IList<DeviceFarmUnitZoneRule> rules = RuleHierarchyResolver.ResolveRelayRules(simulationRules, experimentRules, zoneRules, unitRules, farmRules, globalRules);
                    DateOnly localDate = DateOnly.FromDateTime(DateTime.UtcNow.AddSeconds(utcOffsetSeconds));
                    // Tenant's own site location first, server-wide default otherwise (roadmap #396(6), same cascade as ScheduleTimeZone above) - only falls all the way through when NEITHER tenant nor server has one set.
                    double? lat = tenant?.Latitude ?? serverConfig.WeatherLocationLat;
                    double? lon = tenant?.Longitude ?? serverConfig.WeatherLocationLon;
                    controller.Rules = AstronomicalRuleResolver.Resolve(rules, lat, lon, localDate, utcOffsetSeconds);
                    DeviceFarmUnitZone? zone = await repo.DeviceFarmUnitZoneGetByIdAsync(idZone);
                    controller.WaterPumpMaxRunSeconds = zone?.WaterPumpMaxRunSeconds;
                    controller.WaterPumpCooldownSeconds = zone?.WaterPumpCooldownSeconds;
                    controller.WaterPumpMinLevel = zone?.WaterPumpMinLevel;
                    controller.WaterLevelRawEmpty = zone?.WaterLevelRawEmpty;
                    controller.WaterLevelRawFull = zone?.WaterLevelRawFull;
                    // Computed here as a single AND-NOT gate, not sent as two separate flags - see DeviceConfigController.SkipWaterPumpForRain's remarks.
                    controller.SkipWaterPumpForRain = zone?.SkipWaterPumpWhenRainPredicted == true && serverConfig.WeatherRainPredicted;

                    // Roadmap #219 - only what's still active (not yet past ExpiresAtUtc) rides along; a naturally-expired command simply stops appearing on the next poll, no explicit "stop" needed.
                    IList<DeviceManualOverride> activeOverrides = await repo.ManualOverridesActiveForDeviceAsync(device.IDDevice!.Value);
                    controller.ManualOverrides = activeOverrides.Select(o => new DeviceManualOverridePush
                    {
                        RelayFunction = o.RelayFunction,
                        Mode = o.Mode,
                        ExpiresAtEpoch = o.ExpiresAtUtc.ToUnixTimeSeconds(),
                        TargetMetric = o.TargetMetric,
                        TargetThreshold = o.TargetThreshold,
                        TargetHysteresis = o.TargetHysteresis,
                    }).ToList();
                }
                deviceConfig.DeviceConfigController = controller;
            }

            return deviceConfig;
        }
    }
}
