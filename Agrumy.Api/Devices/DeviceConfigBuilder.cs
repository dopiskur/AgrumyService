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
            // Per-organization, not global - a device with no organization (genuinely unassigned) or an unset zone both fall back to UTC via GetUtcOffsetSeconds' own null handling.
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
                    // Most specific tier, checked ahead of the real hierarchy below - present only when this device is currently a member of an active simulation session, in which case that session's own rules apply first, falling back to the real hierarchy for whatever they don't cover. Shared by both branches - a simulation session isn't itself Greenhouse/Open-Field-specific.
                    int? idSimulationSession = await simulationRepo.DeviceActiveSimulationSessionIdGetAsync(device.IDDevice!.Value);

                    int? idExperiment, idZone = null, idFarmParcelZoneForRules = null, idUnit = null, idSowingForRules = null, idFarm = null;
                    IFarmLeafLevelNode? leafNode;
                    if (device.DeviceFarmUnitZoneID is int zoneId)
                    {
                        idZone = zoneId;
                        // One tier below Simulation; present only when the zone is currently under an active Experiment (Zone>Unit>Farm cascade resolved by ActiveExperimentIdForZoneAsync itself).
                        idExperiment = await experimentRepo.ActiveExperimentIdForZoneAsync(zoneId);
                        idUnit = device.DeviceFarmUnitID;
                        // Farm rules only apply when the device's own Unit is actually assigned to one - a Farm-less Unit sees no Farm-scope rules at all, same "unassigned means no inheritance" rule as Global always applying regardless.
                        idFarm = idUnit is int farmUnitId ? (await deviceFarmUnitRepo.DeviceFarmUnitGetByIdAsync(farmUnitId))?.DeviceFarmID : null;
                        leafNode = await deviceFarmUnitRepo.DeviceFarmUnitZoneGetByIdAsync(zoneId);
                    }
                    else
                    {
                        // Open-Field's FarmParcelZone>Sowing>Farm cascade (restructure R, D5) - a zone with no active sowing gets NO rules at all, not even Farm/Global (D10); the zone's own safety limits (WaterPump/Heating, set below from leafNode) still apply regardless, those aren't rule-scoped.
                        int idParcel = device.FarmParcelZoneID!.Value;
                        leafNode = await farmParcelRepo.FarmParcelZoneGetByIdAsync(idParcel);
                        if (device.SowingID is int sowingId)
                        {
                            idFarmParcelZoneForRules = idParcel;
                            idSowingForRules = sowingId;
                            idExperiment = await experimentRepo.ActiveExperimentIdForFarmParcelZoneAsync(idParcel);
                            idFarm = (await sowingRepo.SowingGetByIdAsync(sowingId))?.FarmID;
                        }
                        else
                        {
                            idExperiment = null;
                        }
                    }
                    // D10 - suppressed alongside the rest of the Open-Field cascade when the zone has no active sowing; the Greenhouse branch always sees Global.
                    bool suppressGlobalRules = device.DeviceFarmUnitZoneID is null && device.SowingID is null;
                    bool includeGlobal = !suppressGlobalRules && device.TenantID != null;

                    // One query in place of up to 6 sequential ones - every id above is resolved first, then the flat result is partitioned back out by each row's own scope FK below.
                    IList<DeviceFarmUnitZoneRule> hierarchyRows = await deviceFarmUnitRepo.RulesGetForHierarchyAsync(
                        device.TenantID ?? 0, idSimulationSession, idExperiment, idZone, idFarmParcelZoneForRules, idUnit, idSowingForRules, idFarm, includeGlobal);

                    IList<DeviceFarmUnitZoneRule> simulationRules = idSimulationSession is int simId
                        ? hierarchyRows.Where(r => r.SimulationSessionID == simId).ToList() : [];
                    IList<DeviceFarmUnitZoneRule> experimentRules = idExperiment is int expId
                        ? hierarchyRows.Where(r => r.ExperimentID == expId).ToList() : [];
                    IList<DeviceFarmUnitZoneRule> leafRules = idZone is int lz ? hierarchyRows.Where(r => r.DeviceFarmUnitZoneID == lz).ToList()
                        : idFarmParcelZoneForRules is int lp ? hierarchyRows.Where(r => r.DeviceFarmParcelZoneID == lp).ToList() : [];
                    IList<DeviceFarmUnitZoneRule> midRules = idUnit is int mu ? hierarchyRows.Where(r => r.DeviceFarmUnitID == mu).ToList()
                        : idSowingForRules is int ms ? hierarchyRows.Where(r => r.DeviceSowingID == ms).ToList() : [];
                    IList<DeviceFarmUnitZoneRule> farmRules = idFarm is int fId ? hierarchyRows.Where(r => r.DeviceFarmID == fId).ToList() : [];
                    IList<DeviceFarmUnitZoneRule> globalRules = includeGlobal
                        ? hierarchyRows.Where(r => r.DeviceFarmID == null && r.DeviceFarmUnitID == null && r.DeviceFarmUnitZoneID == null
                            && r.DeviceSowingID == null && r.DeviceFarmParcelZoneID == null && r.SimulationSessionID == null && r.ExperimentID == null).ToList()
                        : [];
                    IList<DeviceFarmUnitZoneRule> rules = RuleHierarchyResolver.ResolveRelayRules(simulationRules, experimentRules, leafRules, midRules, farmRules, globalRules);
                    DateOnly localDate = DateOnly.FromDateTime(DateTime.UtcNow.AddSeconds(utcOffsetSeconds));
                    // Organization's own site location first, server-wide default otherwise (same cascade as ScheduleTimeZone above) - only falls all the way through when NEITHER organization nor server has one set.
                    double? lat = tenant?.Latitude ?? serverConfig.WeatherLocationLat;
                    double? lon = tenant?.Longitude ?? serverConfig.WeatherLocationLon;
                    controller.Rules = AstronomicalRuleResolver.Resolve(rules, lat, lon, localDate, utcOffsetSeconds);
                    controller.WaterPumpMaxRunSeconds = leafNode?.WaterPumpMaxRunSeconds;
                    controller.WaterPumpCooldownSeconds = leafNode?.WaterPumpCooldownSeconds;
                    controller.WaterPumpMinLevel = leafNode?.WaterPumpMinLevel;
                    controller.WaterLevelRawEmpty = leafNode?.WaterLevelRawEmpty;
                    controller.WaterLevelRawFull = leafNode?.WaterLevelRawFull;
                    // Computed here as a single AND-NOT gate, not sent as two separate flags - see DeviceConfigController.SkipWaterPumpForRain's remarks. Per-organization, so the lookup is skipped entirely unless the zone actually opted in.
                    controller.SkipWaterPumpForRain = leafNode?.SkipWaterPumpWhenRainPredicted == true
                        && device.TenantID is int weatherTenantId
                        && (await tenantRepo.TenantWeatherStateGetAsync(weatherTenantId)).WeatherRainPredicted;
                    controller.HeatingFailSafePolicy = leafNode?.HeatingFailSafePolicy;

                    // Only what's still active (not yet past ExpiresAtUtc) rides along; a naturally-expired command simply stops appearing on the next poll, no explicit "stop" needed.
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
