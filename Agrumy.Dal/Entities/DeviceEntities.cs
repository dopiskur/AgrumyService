namespace Agrumy.Dal.Entities
{
    /// Top-level organizational grouping ABOVE Unit within the same organization (a physical farm/site); optional, a DeviceFarmUnit not yet assigned to one has DeviceFarmID null.
    public class DeviceFarmRow
    {
        public int IDDeviceFarm { get; set; }
        public int? TenantID { get; set; }
        public string? DeviceFarmName { get; set; }
        // See Agrumy.Shared.Models.FarmType - stored as the enum's int value, same convention as Device.LocationSource.
        public int FarmType { get; set; }
        public int DisplayOrder { get; set; }

        // Soft delete, cascades to every DeviceFarmUnit/DeviceFarmUnitZone/Device still assigned to this farm at delete time (see EfDeviceFarmUnitRepository.DeviceFarmDeleteAsync). See AgrumyDbContext's HasQueryFilter on this entity.
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }

        // Still restorable while only Purged (nothing physically removed yet); becomes irreversible once the purge cycle actually reaps it (EfDeviceFarmUnitRepository.DeviceFarmRecycleBinPurgeAsync).
        public bool Purged { get; set; }
        public DateTimeOffset? PurgedAtUtc { get; set; }
    }

    public class DeviceFarmUnitRow
    {
        public int IDDeviceFarmUnit { get; set; }
        public int? TenantID { get; set; }
        public string? DeviceFarmUnitName { get; set; }
        public bool? ZoneEnabled { get; set; }
        // Optional (a Farm-less Unit stays valid, no default-farm backfill).
        public int? DeviceFarmID { get; set; }
        public int DisplayOrder { get; set; }

        // Set only as a cascade of its DeviceFarmRow's own Deleted (never independently) - see AgrumyDbContext's HasQueryFilter on this entity.
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// DeviceFarmUnitID is the real "Unit contains many Zones" FK (see db/migrations/2026-09-02-deviceunit-zone-containment.sql).
    public class DeviceFarmUnitZoneRow
    {
        public int IDDeviceFarmUnitZone { get; set; }
        public int? TenantID { get; set; }
        public int DeviceFarmUnitID { get; set; }
        public string? DeviceFarmUnitZoneName { get; set; }

        // Set only as a cascade of the owning DeviceFarmRow's own Deleted (never independently) - see AgrumyDbContext's HasQueryFilter on this entity.
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }

        // See Agrumy.Shared.Models.DeviceFarmUnitZone's own copy of these for the full explanation.
        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }

        // See Agrumy.Shared.Models.DeviceFarmUnitZone.SkipWaterPumpWhenRainPredicted.
        public bool SkipWaterPumpWhenRainPredicted { get; set; }

        // See Agrumy.Shared.Models.DeviceFarmUnitZone's own copy of these for the full explanation.
        public double? TankCapacityLiters { get; set; }
        public int? WaterLevelRawEmpty { get; set; }
        public int? WaterLevelRawFull { get; set; }
        public DateTimeOffset? TankRefillNotifiedAt { get; set; }
        public double? WaterPumpMinLevel { get; set; }

        // See Agrumy.Shared.Models.DeviceFarmUnitZone's own copy of these for the full explanation.
        public int? HeatingMaxRunSeconds { get; set; }
        public int? VentilationMaxRunSeconds { get; set; }
        public int DashboardGridColumns { get; set; } = 4;

        // See Agrumy.Shared.Models.DeviceFarmUnitZone.HeatingFailSafePolicy - stored as HeatingFailSafePolicyType's own int value, null means Hold (the device's own default).
        public int? HeatingFailSafePolicy { get; set; }

        // See Agrumy.Shared.Models.DeviceFarmUnitZone.DashboardWidgets - JSON array, (de)serialized at the application layer same as DeviceFarmUnitZoneRuleRow.RootConditionJson below. Null/empty means no custom widgets configured.
        public string? DashboardWidgetsJson { get; set; }
    }

    /// See Agrumy.Shared.Models.DeviceFarmUnitZoneRule - RootConditionJson is a single ConditionNode tree, (de)serialized at the application layer, not a native JSON column type. Exactly one of DeviceFarmUnitZoneID/DeviceFarmUnitID is set for Zone/Unit scope, both null for Global (per-organization) scope.
    public class DeviceFarmUnitZoneRuleRow
    {
        public int IDDeviceFarmUnitZoneRule { get; set; }
        public int TenantID { get; set; }
        // Exactly one of DeviceFarmID/DeviceFarmUnitID/DeviceFarmUnitZoneID is set (Farm/Unit/Zone scope); all three null means Global. Enforced in DeviceFarmUnitApiController, not the DB, same as the other two.
        public int? DeviceFarmID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        // Open-Field's Unit/Zone scope equivalents of DeviceFarmUnitID/DeviceFarmUnitZoneID above (restructure R, D5 - Sowing is the mid tier, FarmParcelZone the leaf; FarmParcel itself is never a rule scope).
        public int? DeviceSowingID { get; set; }
        public int? DeviceFarmParcelZoneID { get; set; }
        public int? SimulationSessionID { get; set; }
        public int? ExperimentID { get; set; }
        public int ActionType { get; set; }
        public int? RelayFunction { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string RootConditionJson { get; set; } = "null";
        public int? TargetPercent { get; set; }
        public bool IsSafetyRule { get; set; }
        public string? NotificationSubject { get; set; }
        public string? NotificationBody { get; set; }
    }

    /// Per-(rule, zone) dedup latch for Agrumy.Api.BackgroundWorkers.RuleNotificationEvaluator - a rule scoped above Zone level is evaluated independently against every zone it reaches, so the "already notified, don't re-fire every tick" state is keyed per zone, not just per rule.
    public class RuleNotificationStateRow
    {
        public int IDRuleNotificationState { get; set; }
        public int RuleID { get; set; }
        public int DeviceFarmUnitZoneID { get; set; }
        public bool WasTrue { get; set; }
        public DateTimeOffset? LastFiredAtUtc { get; set; }
    }

    public class DeviceRoleRow
    {
        public int IDDeviceRole { get; set; }
        public string? DeviceRoleName { get; set; }
        public bool? SensorEnabled { get; set; }
        public bool? ControllerEnabled { get; set; }
    }

    public class DeviceTypeServiceRow
    {
        public int IDDeviceTypeService { get; set; }
        public string? ServiceType { get; set; }
    }

    public class DeviceTypeRelayRow
    {
        public int IDDeviceTypeRelay { get; set; }
        public string? RelayName { get; set; }
    }

    public class DeviceTypeSensorRow
    {
        public int IDDeviceTypeSensor { get; set; }
        public string? SensorName { get; set; }
        public string? SensorDescription { get; set; }
        public int? Battery { get; set; }
        public int? Temperature { get; set; }
        public int? TemperatureSoil { get; set; }
        public int? Humidity { get; set; }
        public int? Moisture { get; set; }
        public int? Light { get; set; }
        public int? Co2 { get; set; }
        public int? Tvoc { get; set; }
        public int? Barometer { get; set; }
        public int? WaterPH { get; set; }
        public int? WaterTankLevel { get; set; }
        public int? RainLevel { get; set; }
        public int? Wind { get; set; }
        public int? Ec { get; set; }
        public int? Weight { get; set; }
    }

    public class DeviceConfigControllerRow
    {
        public int IDDeviceConfigController { get; set; }
        public double? TempLow { get; set; }
        public double? TempHigh { get; set; }
        public double? HumidLow { get; set; }
        public double? HumidHigh { get; set; }
        public double? MoistLow { get; set; }
        public double? MoistHigh { get; set; }
        public double? LightLow { get; set; }
        public double? LightHigh { get; set; }
        public double? WaterLow { get; set; }
        public double? WaterHigh { get; set; }

        // Hysteresis (dead zone) margins - see Agrumy.Shared.Models.DeviceConfigController.
        public double? WaterLevelHysteresis { get; set; }
        public double? TemperatureHysteresis { get; set; }
        public double? HumidityHysteresis { get; set; }
        public double? LightHysteresis { get; set; }

        // Default member initializers match the DB column defaults (all 0/false).
        public bool? VentilationIntervalEnabled { get; set; } = false;
        public int? VentilationInterval { get; set; } = 0;
        public int? VentilationIntervalLength { get; set; } = 0;
        public bool? LightIntervalEnabled { get; set; } = false;
        public int? LightInterval { get; set; } = 0;
        public int? LightIntervalLength { get; set; } = 0;
        public bool? HeatingIntervalEnabled { get; set; } = false;
        public int? HeatingInterval { get; set; } = 0;
        public int? HeatingIntervalLength { get; set; } = 0;
        public bool? WaterPumpIntervalEnabled { get; set; } = false;
        public int? WaterPumpInterval { get; set; } = 0;
        public int? WaterPumpIntervalLength { get; set; } = 0;

        // See Agrumy.Shared.Models.DeviceConfigController.WaterPumpMaxRunSeconds/WaterPumpCooldownSeconds.
        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }

        public bool? RelayEnabled { get; set; }
    }

    /// One physically-wired relay slot assigned to a RelayFunction - only assigned slots get a row, replacing the fixed Relay1..Relay8 columns that used to live on DeviceConfigControllerRow.
    public class DeviceConfigControllerRelayRow
    {
        public int IDDeviceConfigController { get; set; }
        public int Slot { get; set; }
        public int RelayFunction { get; set; }
        public int OutputKind { get; set; } = 1; // Agrumy.Shared.Models.OutputKind.Relay - migration default, every existing row behaves exactly as it always did
        public int? PairSlot { get; set; }
        public int? TravelSeconds { get; set; }
        public int? DeadTimeSeconds { get; set; }
        public int? PwmFrequencyHz { get; set; }
        public int? ServoMinPulseUs { get; set; }
        public int? ServoMaxPulseUs { get; set; }
        public int? ServoSafePositionPercent { get; set; }
        public int? LatchingPulseMs { get; set; }
        public int? RateLimitPercentPerSecond { get; set; }
        public int? MinOnSeconds { get; set; }
        public int? MinOffSeconds { get; set; }
        public int? TimeProportioningPeriodSeconds { get; set; }
    }

    /// One per RelayFunctionType with a non-default control mode - only a Pid-mode function gets a row, a function with no row stays Threshold. See Agrumy.Shared.Models.DeviceFunctionControl.
    public class DeviceConfigControllerFunctionControlRow
    {
        public int IDDeviceConfigController { get; set; }
        public int RelayFunction { get; set; }
        public int ControlMode { get; set; }
        public int? PidSetpointMetric { get; set; }
        public double? PidSetpoint { get; set; }
        public double? PidKp { get; set; }
        public double? PidKi { get; set; }
        public double? PidKd { get; set; }
        public double? PidSampleIntervalSeconds { get; set; }
    }


    public class DeviceConfigSensorRow
    {
        public int IDDeviceConfigSensor { get; set; }
        public int? SensorBattery { get; set; }
        // See Agrumy.Shared.Models.DeviceConfigSensor.BatteryDividerR1/R2.
        public double? BatteryDividerR1 { get; set; }
        public double? BatteryDividerR2 { get; set; }
        public int? SensorTemp { get; set; }
        public int? SensorTempSoil { get; set; }
        public int? SensorHumid { get; set; }
        public int? SensorMoist { get; set; }
        public int? SensorLight { get; set; }
        public int? SensorCo2 { get; set; }
        public int? SensorTvoc { get; set; }
        public int? SensorBarometer { get; set; }
        public int? SensorPH { get; set; }
        public int? SensorRainLevel { get; set; }
        public int? SensorWaterLevel { get; set; }
        public int? SensorWind { get; set; }
        public int? SensorEc { get; set; }
        public int? SensorWeight { get; set; }
        public double? WeightCalibrationFactor { get; set; }
        public long? WeightTareOffset { get; set; }
        public double? EcCalibrationSlope { get; set; }
        public double? EcCalibrationOffset { get; set; }
    }

    /// Per-metric sensor-reading overrides for an EXISTING physical device (Simulation Mode) - one row per device, upserted from the Web Simulation page. A null field means "use the real reading"; Enabled=false means every field is ignored regardless of value.
    public class DeviceSimulationRow
    {
        public int DeviceID { get; set; }
        public bool Enabled { get; set; }
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        public int? Battery { get; set; }
        public int? Moisture { get; set; }
        public int? Light { get; set; }
        public int? Co2 { get; set; }
        public int? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public int? RainLevel { get; set; }
        public int? WaterLevel { get; set; }
        public int? Wind { get; set; }

        // See Agrumy.Shared.Models.DeviceSimulation.Latitude/Longitude.
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    /// Purely a server-internal registry of which device rows VirtualDeviceRunnerBackgroundService is responsible for driving - never exposed on any wire contract, never read by the device-facing endpoints themselves (Register/Authenticate/Config/SensorData/ControllerData have no idea a caller is virtual). A device with no row here is an ordinary, real device.
    public class DeviceVirtualRow
    {
        public int DeviceID { get; set; }
        public DateTimeOffset DateCreated { get; set; }
    }

    /// The "Add Simulation" container/entry-point a device (physical or virtual) is added TO, replacing the old per-device toggle mental model. ExpiresAtUtc is StartedAtUtc plus an admin-chosen duration, hard-capped at 48h; StoppedAtUtc null means still running, set either by an explicit early stop or by SimulationSessionExpiryEvaluator once ExpiresAtUtc passes.
    public class SimulationSessionRow
    {
        public int IDSimulationSession { get; set; }
        public int TenantID { get; set; }
        public string? Name { get; set; }
        // Nullable now, see Agrumy.Shared.Models.SimulationSession's own copy for the full explanation.
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? ExpiresAtUtc { get; set; }
        public DateTimeOffset? StoppedAtUtc { get; set; }
    }

    /// One device's membership in one SimulationSessionRow - a physical device's actual override values still live in DeviceSimulationRow (this just tracks which session "owns" turning that override on/off and when); a virtual device's presence here is what VirtualDeviceRunnerBackgroundService now checks before driving it at all.
    public class SimulationSessionDeviceRow
    {
        public int IDSimulationSession { get; set; }
        public int DeviceID { get; set; }
        /// Null for a device added individually; set when this membership came from a whole-Unit/Zone SimulationGroupRow add instead, so removing/editing that group only ever touches the devices it actually put here.
        public int? IDSimulationGroup { get; set; }
    }

    /// A whole Unit or Zone added to a simulation session together (as opposed to one device at a time) - one set of override values fanned out to every member device's own DeviceSimulationRow, tracked here as a single editable/removable unit instead of per-device.
    public class SimulationGroupRow
    {
        public int IDSimulationGroup { get; set; }
        public int IDSimulationSession { get; set; }
        /// 1=Unit, 2=Zone (Agrumy.Shared.Models.SimulationGroupScope) - which kind of id ScopeID below is.
        public int Scope { get; set; }
        public int ScopeID { get; set; }
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        public int? Battery { get; set; }
        public int? Moisture { get; set; }
        public int? Light { get; set; }
        public int? Co2 { get; set; }
        public int? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public int? RainLevel { get; set; }
        public int? WaterLevel { get; set; }
        public int? Wind { get; set; }
    }

    public class DeviceRow
    {
        public int IDDevice { get; set; }
        // Nullable, matching the DB column (now nullable, no DEFAULT). TenantID=0 stays a real organization (the bootstrap/default one); null means genuinely unassigned.
        public int? TenantID { get; set; }
        public int? DeviceRoleID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        // Open-Field's Unit/Zone equivalents (restructure R) - SowingID has a real DB FK, FarmParcelZoneID does not (mirrors DeviceFarmUnitID/DeviceFarmUnitZoneID above). SowingID mirrors FarmParcelZone.CurrentSowingID; kept in sync by StartSowing/CloseSowing, null when the zone has no active sowing.
        public int? SowingID { get; set; }
        public int? FarmParcelZoneID { get; set; }
        public int? DeviceConfigSensorID { get; set; }
        public int? DeviceConfigControllerID { get; set; }
        public int? DeviceTypeServiceID { get; set; }
        public string? DeviceName { get; set; }
        public string? MacAddress { get; set; }
        // Admin-set fallback for a device whose firmware build never reports a Kit (generic esp32dev/esp32s3usbotg) - BuildFleetStatusesAsync's ControllerCapable check falls back to this only when the diagnostic DeviceTypeID is unset. Real FK to deviceType.IDDeviceType, not the Kit string.
        public int? ManualDeviceTypeID { get; set; }
        public string ApiId { get; set; } = "";
        public string ApiKey { get; set; } = "";
        // DB-backed fallback for DeviceSessionHandler's cache-miss path - re-validates a session the in-process cache lost (restart/redeploy/wrong instance) without forcing a fresh Authenticate; never mapped onto Device/DeviceDto, same secret-tier as ApiKey.
        public string? ApiAuthToken { get; set; }
        public DateTimeOffset? ApiAuthExpiresAtUtc { get; set; }
        public string? ServicePoint { get; set; }
        public string? ServicePublicKey { get; set; }
        public int? SleepSeconds { get; set; }
        public bool? SleepDeepEnabled { get; set; }
        // See Agrumy.Shared.Models.Device.LoRaGatewayEnabled.
        public bool? LoRaGatewayEnabled { get; set; }
        public bool? DeviceSensorEnabled { get; set; }
        public bool? DeviceControllerEnabled { get; set; }
        public bool? BatteryEnabled { get; set; }
        public bool? Enabled { get; set; }
        public bool? Debug { get; set; }
        public bool? FirmwareUpdate { get; set; }
        public string? FirmwareTargetVersion { get; set; } // See Agrumy.Shared.Models.Device.FirmwareTargetVersion.
        public int? ConfigVersion { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
        public DateTimeOffset? DateModified { get; set; }

        // See Agrumy.Shared.Models.Device.Latitude/Longitude/LocationSource - stored as the enum's int value, same convention as GatewayProfile below.
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int LocationSource { get; set; }

        public bool IsGateway { get; set; }
        public int? GatewayProfile { get; set; }

        public DateTimeOffset? LastFullConfigSentAt { get; set; } // See Agrumy.Shared.Models.Device.LastFullConfigSentAt.

        public string? LastSensorDetectionResult { get; set; } // See Agrumy.Shared.Models.Device.LastSensorDetectionResult.
        public DateTimeOffset? LastSensorDetectionAt { get; set; }

        // LoRa private-protocol uplink encryption key - null until an admin generates one via DeviceApiController.LoRaPrivateKeyGenerate. 64 hex chars = AES-256's 32 raw bytes.
        public string? LoRaPrivateKeyHex { get; set; }

        // Soft delete, see AgrumyDbContext's HasQueryFilter on this entity. Only RecycleBinApiController ever sees a Deleted row directly (IgnoreQueryFilters).
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }

        // Still restorable while only Purged (nothing physically removed yet); becomes irreversible once the purge cycle actually reaps it (EfDeviceRepository.DeviceRecycleBinPurgeAsync).
        public bool Purged { get; set; }
        public DateTimeOffset? PurgedAtUtc { get; set; }
    }

    /// One v2 boot session's replay-protection state (LoRaPrivatePayloadCrypto's per-boot HKDF session key never repeats a (bootNonce, counter) pair by construction; this table is what actually enforces that server-side). PK (DeviceID, BootNonceHex) - a device gets a fresh row every reboot, so EfDeviceRepository.DeviceLoRaSessionAcceptAsync caps it at the 32 most recent per device.
    public class DeviceLoRaSessionRow
    {
        public int DeviceID { get; set; }
        public string BootNonceHex { get; set; } = "";
        public long MaxCounter { get; set; }
        public DateTimeOffset FirstSeenUtc { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; }
    }

    /// One LoRaWAN end-device's DevEUI mapped to the Agrumy device (ApiId/ApiKey) a LoRaGateway acts on behalf of for that DevEUI's uplinks.
    public class GatewayDeviceMappingRow
    {
        public int IDGatewayDeviceMapping { get; set; }
        public int IDGatewayDevice { get; set; }
        public string DevEUI { get; set; } = "";
        public int IDDevice { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }

    /// One scanning device's sighting of one nearby Agrumy_ AP during a discovery scan - raw reports, not yet deduplicated/best-picked (that lives in the repository query layer).
    public class DeviceDiscoveryReportRow
    {
        public int IDReport { get; set; }
        public int ScanningDeviceID { get; set; }
        public string DiscoveredApMac { get; set; } = "";
        public int? Rssi { get; set; }
        public DateTimeOffset DateReported { get; set; }
    }

    /// One discrete, one-shot device delivery item (a real command, a ConfigChanged signal, or a HardReset signal) - see Agrumy.Shared.Models.CommandStatus for why Acknowledged is a real, persisted state.
    public class DeviceOutboxRow
    {
        public int IDDeviceOutbox { get; set; }
        public int DeviceID { get; set; }
        public int Type { get; set; }
        public int Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? ExecutedAt { get; set; }
        public int? ActiveKey { get; set; } // Mirrors Type while active, NULL once terminal; backs the unique (DeviceID, ActiveKey) index IssueCommandAsync's dedup relies on.
        public string? Payload { get; set; }
        public DateTimeOffset? PublishedAt { get; set; } // Set once DeviceOutboxDispatchEvaluator (or a synchronous issue-path publish) has attempted an MQTT push for this row - null means still owed a dispatch attempt.
    }

    /// One active manual actuation - upserted on (DeviceID, RelayFunction), so starting a new command for an already-active function replaces it rather than stacking rows.
    public class DeviceManualOverrideRow
    {
        public int IDDeviceManualOverride { get; set; }
        public int DeviceID { get; set; }
        public int TenantID { get; set; }
        public int RelayFunction { get; set; }
        public int Mode { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public int? TargetMetric { get; set; }
        public double? TargetThreshold { get; set; }
        public double? TargetHysteresis { get; set; }
    }

    /// One row per device, upserted on every config poll (the poll itself is the heartbeat) - keyed by DeviceID (1:1 with device), not an identity column, so the upsert is a plain read-or-insert.
    public class DeviceDiagnosticRow
    {
        public int DeviceID { get; set; }
        public int? TenantID { get; set; }
        public DateTimeOffset? LastSeenAt { get; set; }
        public long? UptimeSeconds { get; set; }
        public int? RssiDbm { get; set; }
        public long? FreeHeapBytes { get; set; }
        // Set by SensorDataApiController.Post on every accepted push (not the config-poll heartbeat, which runs on a different cadence) - TenantQuotaEnforcer.CheckSensorPushIntervalAsync compares this against MinSensorIntervalMinutes to catch a device (compromised, buggy, or just ignoring its own configured sleepSeconds) pushing telemetry faster than its organization's quota allows.
        public DateTimeOffset? LastSensorPushAt { get; set; }
        // Minimum ever recorded since boot (ESP.getMinFreeHeap()), largest single allocatable block (ESP.getMaxAllocHeap(), low value flags fragmentation even when FreeHeapBytes looks fine), and the loop task's unused stack margin (uxTaskGetStackHighWaterMark, low value flags an approaching stack overflow).
        public long? MinFreeHeapBytes { get; set; }
        public long? MaxAllocHeapBytes { get; set; }
        public long? StackHighWaterMarkBytes { get; set; }
        public long? NetworkStackHighWaterMarkBytes { get; set; } // Same as StackHighWaterMarkBytes but for the persistent network task's own stack, not the loop task's.
        // Which CONFIG_SCHEMA_VERSION the device's currently-running firmware understands; lets an admin spot a device whose firmware needs an OTA before the server's own schema moves further ahead.
        public int? ConfigSchemaVersion { get; set; }
        public DateTimeOffset? OfflineNotifiedAt { get; set; } // When OfflineAlertBackgroundService last notified admins about the device's current offline streak; one notification per streak, not per tick.
        public DateTimeOffset? LowBatteryNotifiedAt { get; set; } // Same dedup-by-streak rule as OfflineNotifiedAt, but for LowBatteryAlertEvaluator.
        public string? FirmwareVersion { get; set; }
        public string? Board { get; set; } // See Agrumy.Shared.Models.DeviceConfigPoll.Board.
        // Real FK to deviceType.IDDeviceType, resolved from the firmware-reported Kit string (Agrumy.Shared.Models.DeviceConfigPoll.Kit) by DeviceDiagnosticUpsertAsync - the wire protocol still carries a string, only storage is numeric.
        public int? DeviceTypeID { get; set; }
    }

    /// Catalog of recognized physical device kits - IDDeviceType is the real PK, Kit a unique display/build-flag string (e.g. "KC868-A6") every referencing table now FKs to by id, not by name. PinoutJson is an unopinionated per-kit GPIO layout blob, null for a kit nobody has documented pinout for yet, including every auto-registered one (see DeviceDiagnosticUpsertAsync).
    public class DeviceTypeRow
    {
        public int IDDeviceType { get; set; }
        public string Kit { get; set; } = "";
        public bool ControllerCapable { get; set; }
        public string? PinoutJson { get; set; }
    }

    // Board/Source/FileName/SizeBytes/Sha256/PublishedAt - see Agrumy.Shared.Models.DeviceFirmware for what each means; DeviceTypeID is the legacy key.
    public class DeviceFirmwareRow
    {
        public int IDDeviceFirmware { get; set; }
        public int? DeviceTypeID { get; set; }
        public string? Board { get; set; }
        public string? Version { get; set; }
        public string? Url { get; set; }
        public int Source { get; set; }
        public string? FileName { get; set; }
        public long? SizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public DateTimeOffset? DateAdded { get; set; }

        // See Agrumy.Shared.Models.DeviceFirmware's own copy of these for the full explanation.
        public string? FullImageFileName { get; set; }
        public string? FullImageUrl { get; set; }
        public long? FullImageSizeBytes { get; set; }
        public string? FullImageSha256 { get; set; }
    }
}
