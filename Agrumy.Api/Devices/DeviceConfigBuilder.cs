using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Agrumy.Rules;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;

namespace Agrumy.Api.Devices
{
    /// Builds the DeviceConfig body a Config poll or Register response sends back, shared so GatewayApiController.Batch's Config entries produce byte-for-byte the same response as a direct POST /api/Device/Config.
    public class DeviceConfigBuilder(IServerConfigRepository serverConfigRepo, ITenantRepository tenantRepo, IDeviceRepository deviceRepo, ISimulationRepository simulationRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IFarmParcelRepository farmParcelRepo, ISowingRepository sowingRepo, IExperimentRepository experimentRepo, FirmwareCatalogService firmwareCatalog, DeviceOutboxService outboxService)
    {
        /// Whether GetConfig/RunConfigAsync must send a full config this poll: a pending ConfigChanged outbox signal, a pending actionable command, or - because BuildAsync recomputes UtcOffsetSeconds/SkipWaterPumpForRain fresh every call without either ever consuming a signal for it - the periodic heartbeat window has elapsed since the device's last full send. Not used by Register, which always sends a fresh config unconditionally.
        public async Task<bool> NeedsRefreshAsync(Device device, bool configChangePending, PendingCommand? pendingCommand)
        {
            if (configChangePending || pendingCommand != null)
            {
                return true;
            }
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            if (serverConfig.ConfigHeartbeatHours <= 0)
            {
                return false;
            }
            return device.LastFullConfigSentAt is not DateTimeOffset last
                || (DateTimeOffset.UtcNow - last).TotalHours >= serverConfig.ConfigHeartbeatHours;
        }

        public async Task<DeviceConfig> BuildAsync(Device device, PendingCommand? pendingCommand, string? board)
        {
            // Computed fresh (not cached) every response so a DST shift or ScheduleTimeZone change reaches every device on its next poll; also reused below as the server-wide location fallback.
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            // Per-tenant, not global - a device with no tenant (roadmap #406, genuinely unassigned) or an unset zone both fall back to UTC via GetUtcOffsetSeconds' own null handling.
            Tenant? tenant = device.TenantID is int tenantId ? await tenantRepo.TenantGetByIdAsync(tenantId) : null;
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
                FirmwareUpdate = device.FirmwareUpdate,
                Enabled = device.Enabled,
                EmergencyStop = tenant?.EmergencyStopActive == true,
                PendingCommand = pendingCommand,
                SimulationModeEnabled = (await deviceRepo.DeviceSimulationGetAsync(device.IDDevice!.Value))?.Enabled == true,
            };

            // Consumed unconditionally - BuildAsync only ever runs when NeedsRefreshAsync said yes, so any pending ConfigChanged signal is satisfied by this send regardless of which of its three reasons actually triggered it; a no-op when nothing was pending.
            await outboxService.ConsumePendingConfigChangeAsync(device.IDDevice!.Value);

            // Fire-once, cleared the instant it's included rather than waiting for a confirmation that can never come back - a device told to reset() wipes itself and restarts before it could ever report anything, so "wait for the device to confirm" (FirmwareUpdate's pattern) would leave this stuck true and re-trigger on every future poll after the device re-registers.
            deviceConfig.Reset = await outboxService.ConsumeHardResetIfPendingAsync(device.IDDevice!.Value);

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
                deviceConfig.DeviceConfigSensor = await deviceRepo.DeviceConfigSensorGetAsync(device.DeviceConfigSensorID);
            }
            if (deviceConfig.DeviceControllerEnabled == true)
            {
                // Relay-pin mapping comes from the device row, but Rules/safety limits come from its zone, merged into the same DeviceConfigController; no zone means an empty Rules list so every relay stays off.
                DeviceConfigController? controller = await deviceRepo.DeviceConfigControllerGetAsync(device.DeviceConfigControllerID);
                if (controller != null && (device.DeviceFarmUnitZoneID is int || device.FarmParcelZoneID is int))
                {
                    // Most specific tier, checked ahead of the real hierarchy below - empty unless this device is currently a member of an active simulation session, in which case that session's own rules apply first, falling back to the real hierarchy for whatever they don't cover. Shared by both branches - a simulation session isn't itself Greenhouse/Open-Field-specific.
                    IList<DeviceFarmUnitZoneRule> simulationRules = await simulationRepo.DeviceActiveSimulationSessionIdGetAsync(device.IDDevice!.Value) is int idSession
                        ? await deviceFarmUnitRepo.RulesGetForSimulationAsync(idSession) : [];

                    IList<DeviceFarmUnitZoneRule> experimentRules, leafRules, midRules, farmRules;
                    IFarmLeafLevelNode? leafNode;
                    if (device.DeviceFarmUnitZoneID is int idZone)
                    {
                        // One tier below Simulation; empty unless the zone is currently under an active Experiment (Zone>Unit>Farm cascade resolved by ActiveExperimentIdForZoneAsync itself).
                        experimentRules = await experimentRepo.ActiveExperimentIdForZoneAsync(idZone) is int idExperiment
                            ? await deviceFarmUnitRepo.RulesGetForExperimentAsync(idExperiment) : [];
                        leafRules = await deviceFarmUnitRepo.RulesGetForZoneAsync(idZone);
                        midRules = device.DeviceFarmUnitID is int idUnit ? await deviceFarmUnitRepo.RulesGetForUnitAsync(idUnit) : [];
                        // Farm rules only apply when the device's own Unit is actually assigned to one - a Farm-less Unit sees no Farm-scope rules at all, same "unassigned means no inheritance" rule as Global always applying regardless.
                        farmRules = device.DeviceFarmUnitID is int farmUnitId
                            && (await deviceFarmUnitRepo.DeviceFarmUnitGetByIdAsync(farmUnitId))?.DeviceFarmID is int idFarm
                            ? await deviceFarmUnitRepo.RulesGetForFarmAsync(idFarm) : [];
                        leafNode = await deviceFarmUnitRepo.DeviceFarmUnitZoneGetByIdAsync(idZone);
                    }
                    else
                    {
                        // Open-Field's FarmParcelZone>Sowing>Farm cascade (restructure R, D5) - a zone with no active sowing gets NO rules at all, not even Farm/Global (D10); the zone's own safety limits (WaterPump/Heating, set below from leafNode) still apply regardless, those aren't rule-scoped.
                        int idParcel = device.FarmParcelZoneID!.Value;
                        leafNode = await farmParcelRepo.FarmParcelZoneGetByIdAsync(idParcel);
                        if (device.SowingID is int idSowing)
                        {
                            experimentRules = await experimentRepo.ActiveExperimentIdForFarmParcelZoneAsync(idParcel) is int idExperiment
                                ? await deviceFarmUnitRepo.RulesGetForExperimentAsync(idExperiment) : [];
                            leafRules = await deviceFarmUnitRepo.RulesGetForFarmParcelZoneAsync(idParcel);
                            midRules = await deviceFarmUnitRepo.RulesGetForSowingAsync(idSowing);
                            farmRules = (await sowingRepo.SowingGetByIdAsync(idSowing))?.FarmID is int idFarm
                                ? await deviceFarmUnitRepo.RulesGetForFarmAsync(idFarm) : [];
                        }
                        else
                        {
                            experimentRules = [];
                            leafRules = [];
                            midRules = [];
                            farmRules = [];
                        }
                    }
                    // D10 - suppressed alongside the rest of the Open-Field cascade when the zone has no active sowing; the Greenhouse branch always sees Global.
                    bool suppressGlobalRules = device.DeviceFarmUnitZoneID is null && device.SowingID is null;
                    IList<DeviceFarmUnitZoneRule> globalRules = !suppressGlobalRules && device.TenantID is int globalTenantId ? await deviceFarmUnitRepo.RulesGetForTenantGlobalAsync(globalTenantId) : [];
                    IList<DeviceFarmUnitZoneRule> rules = RuleHierarchyResolver.ResolveRelayRules(simulationRules, experimentRules, leafRules, midRules, farmRules, globalRules);
                    DateOnly localDate = DateOnly.FromDateTime(DateTime.UtcNow.AddSeconds(utcOffsetSeconds));
                    // Tenant's own site location first, server-wide default otherwise (roadmap #396(6), same cascade as ScheduleTimeZone above) - only falls all the way through when NEITHER tenant nor server has one set.
                    double? lat = tenant?.Latitude ?? serverConfig.WeatherLocationLat;
                    double? lon = tenant?.Longitude ?? serverConfig.WeatherLocationLon;
                    controller.Rules = AstronomicalRuleResolver.Resolve(rules, lat, lon, localDate, utcOffsetSeconds);
                    controller.WaterPumpMaxRunSeconds = leafNode?.WaterPumpMaxRunSeconds;
                    controller.WaterPumpCooldownSeconds = leafNode?.WaterPumpCooldownSeconds;
                    controller.WaterPumpMinLevel = leafNode?.WaterPumpMinLevel;
                    controller.WaterLevelRawEmpty = leafNode?.WaterLevelRawEmpty;
                    controller.WaterLevelRawFull = leafNode?.WaterLevelRawFull;
                    // Computed here as a single AND-NOT gate, not sent as two separate flags - see DeviceConfigController.SkipWaterPumpForRain's remarks. Per-tenant, so the lookup is skipped entirely unless the zone actually opted in.
                    controller.SkipWaterPumpForRain = leafNode?.SkipWaterPumpWhenRainPredicted == true
                        && device.TenantID is int weatherTenantId
                        && (await tenantRepo.TenantWeatherStateGetAsync(weatherTenantId)).WeatherRainPredicted;
                    controller.HeatingFailSafePolicy = leafNode?.HeatingFailSafePolicy;

                    // Roadmap #219 - only what's still active (not yet past ExpiresAtUtc) rides along; a naturally-expired command simply stops appearing on the next poll, no explicit "stop" needed.
                    IList<DeviceManualOverride> activeOverrides = await deviceFarmUnitRepo.ManualOverridesActiveForDeviceAsync(device.IDDevice!.Value);
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
