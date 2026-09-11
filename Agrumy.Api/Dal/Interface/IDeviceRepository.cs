using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// The minimal shape OfflineAlertBackgroundService needs - not the full DeviceFleetStatus, since OfflineNotifiedAt is alert-bookkeeping the Web UI has no business displaying.
    public sealed record OfflineAlertCandidate(
        int IDDevice,
        int? TenantID,
        string? DeviceName,
        int? SleepSeconds,
        DateTimeOffset? LastSeenAt,
        DateTimeOffset? OfflineNotifiedAt);

    /// The minimal shape LowBatteryAlertEvaluator needs - Battery is the latest telemetry reading (not the heartbeat, see DeviceFleetStatus), null when never reported.
    public sealed record LowBatteryAlertCandidate(
        int IDDevice,
        int? TenantID,
        string? DeviceName,
        int? Battery,
        DateTimeOffset? LowBatteryNotifiedAt);

    /// The minimal shape FrostAlertEvaluator needs for its local DewPoint-spread confirmation signal - latest Temperature+Humidity per enabled device, no tenant scoping since the underlying forecast check is itself install-wide.
    public sealed record FrostSensorReading(double? Temperature, double? Humidity);

    /// Device facet of the data layer: device CRUD, sensor/controller configs, firmware (OTA), the fixed type lists, and device events.
    public interface IDeviceRepository
    {
        /// Returns the created device (with its generated IDDevice) directly - callers don't need a follow-up DeviceGetAsync. Null quotaCheckAsync (e.g. a virtual/simulation device, or admin data import) skips the quota check and its Serializable transaction entirely - see QuotaGuard.
        Task<Device> DeviceAddAsync(Device device, Func<Task<string?>>? quotaCheckAsync = null);

        /// Roadmap #409 - soft delete; see AgrumyDbContext's HasQueryFilter on DeviceRow. Use DeviceRecycleBinGetAsync/DeviceRestoreAsync to see/undo it.
        Task DeviceDeleteAsync(int? idDevice, int? tenantID);

        /// Every soft-deleted, not-yet-Purged device (tenantID null = every tenant) - still visible/restorable in the Recycle Bin listing.
        Task<IList<Device>> DeviceRecycleBinGetAsync(int? tenantID);

        /// Ownership-check lookup for a soft-deleted device (no tenant filter, Purged or not) - null if the device doesn't exist or isn't deleted.
        Task<Device?> DeviceRecycleBinGetByIdAsync(int idDevice);

        /// Every Deleted AND Purged device (tenantID null = every tenant) - marked for permanent removal but still restorable until the purge cycle actually reaps it.
        Task<IList<Device>> DevicePendingPurgeGetAsync(int? tenantID);

        /// Undoes DeviceDeleteAsync (or a pending DeviceRecycleBinMarkPurgedAsync) - clears BOTH Deleted and Purged, false if the device doesn't exist, isn't deleted, or belongs to a different tenant.
        Task<bool> DeviceRestoreAsync(int idDevice, int? tenantID);

        /// Roadmap #427 - marks an already soft-deleted device for permanent removal without waiting out its tenant's RecycleBinRetentionDays; still fully restorable via DeviceRestoreAsync until the purge cycle actually runs. False if it doesn't exist, isn't deleted, or belongs to a different tenant.
        Task<bool> DeviceRecycleBinMarkPurgedAsync(int idDevice, int? tenantID);

        /// The automatic half of marking - every Deleted, not-yet-Purged device whose OWNING TENANT's effective retention (its own override, falling back to serverDefaultRetentionDays) has elapsed. Returns how many were marked.
        Task<int> DeviceRecycleBinMarkPurgedByRetentionAsync(int serverDefaultRetentionDays, CancellationToken ct);

        /// Every device currently marked Purged, with its owning tenant - the reap step's worklist (DeviceRecycleBinPurgeAsync needs the tenant for its own ownership check).
        Task<IList<(int IDDevice, int? TenantID)>> DevicePurgedIdsGetAsync();

        /// The actual, irreversible removal: every row tied to this device INCLUDING SensorData - false if the device doesn't exist, isn't Deleted+Purged, or belongs to a different tenant. Only ever called once Purged is set (DeviceRecycleBinMarkPurgedAsync or the farm-wide cascade), never directly from an API action.
        Task<bool> DeviceRecycleBinPurgeAsync(int idDevice, int? tenantID);

        /// The device matched by id / apiId / macAddress within the tenant, or null if none matches (or no key was given).
        Task<Device?> DeviceGetAsync(int? tenantID, int? idDevice, string? apiId, string? macAddress);

        /// The device with this id (no tenant filter) - used only for ownership checks before an authorized write - or null if none.
        Task<Device?> DeviceGetByIdAsync(int? idDevice);

        /// The device with this globally unique ApiId (no tenant filter), or null if none. Device-comm endpoints authenticate by ApiId/ApiKey and have no tenant context.
        Task<Device?> DeviceGetByApiIdAsync(string? apiId);
        Task<IList<Device>> DevicesGetAsync(int? tenantID);

        /// Every device in every tenant - callers must check CallerReadsDevicesGlobally themselves.
        Task<IList<Device>> DevicesGetAllAsync();

        /// Every sensor-only device (DeviceSensorEnabled, not DeviceControllerEnabled) in the tenant, or every tenant when null - excludes controller-capable devices since a WiFi.scanNetworks() pause would disrupt their real-time relay duties.
        Task<IList<Device>> DevicesSensorOnlyGetAsync(int? tenantID);
        Task<bool> DeviceCheckMacAddressAsync(int? tenantID, string? macAddress);
        Task<DeviceConfigSensor?> DeviceConfigSensorGetAsync(int? deviceConfigSensorID);
        Task<DeviceConfigController?> DeviceConfigControllerGetAsync(int? deviceConfigControllerID);

        /// The device owning this sensor/controller config id (no tenant filter) - for ownership checks before returning config data - or null if none.
        Task<Device?> DeviceGetByDeviceConfigSensorIdAsync(int? deviceConfigSensorID);
        Task<Device?> DeviceGetByDeviceConfigControllerIdAsync(int? deviceConfigControllerID);

        /// Newest published firmware for a device type (by DateAdded), or null if none.
        Task<DeviceFirmware?> DeviceFirmwareLatestGetAsync(int? deviceTypeID);

        // Device UPDATE
        Task DeviceUpdateAsync(Device? device);
        /// Returns null on success, or a validation error message (caller returns 400) without writing anything.
        Task<string?> DeviceConfigControllerUpdateAsync(int? idDevice, DeviceConfigController? deviceConfigController);
        Task DeviceConfigSensorUpdateAsync(int? iDDevice, DeviceConfigSensor? deviceConfigSensor);

        // Device fixed lists
        Task<IList<DeviceRole>> DeviceRoleGetAsync();

        /// The full deviceType catalog, curated entries and auto-registered ones alike.
        Task<IList<DeviceType>> DeviceTypeGetAsync();
        Task<IList<DeviceTypeService>> DeviceTypeServiceGetAsync();
        Task<IList<DeviceTypeRelay>> DeviceTypeRelayGetAsync();
        Task<IList<DeviceTypeSensor>> DeviceTypeSensorGetAsync();

        // Device diagnostics / fleet

        /// Records the diagnostics from a device's config poll - LastSeenAt is set to the server clock, making the poll itself the heartbeat; null fields still bump LastSeenAt without erasing earlier values.
        Task DeviceDiagnosticUpsertAsync(int deviceID, int tenantID, DeviceConfigPoll poll);

        /// Atomically checks-and-stamps LastSensorPushAt in one guarded UPDATE (the WHERE clause IS the too-frequent check, same shape as DeviceLoRaSessionAcceptAsync) - true (and stamped) only when enough time has passed since the last accepted push; a concurrent push for the same device can't both slip through the way a plain read-then-write would allow.
        Task<bool> DeviceCheckAndRecordSensorPushAsync(int deviceID, TimeSpan minInterval);

        /// Stamps the device row with when a full DeviceConfig body was actually sent - drives DeviceConfigBuilder.NeedsRefreshAsync's periodic heartbeat resend (ServerConfig.ConfigHeartbeatHours).
        Task DeviceMarkConfigSentAsync(int deviceID, DateTime sentAtUtc);

        /// Persists the session token DeviceApiController.Authenticate just cached, so DeviceSessionHandler's cache-miss fallback survives a server restart without every device redoing a full Authenticate. Null token clears it (never actually called with null today - a session simply expires via ExpiresAtUtc).
        Task DeviceSessionSetAsync(int deviceID, string? token, DateTimeOffset? expiresAtUtc);

        /// DeviceSessionHandler's cache-miss fallback read - null when never authenticated or the row's ApiAuthToken was cleared; an ExpiresAtUtc in the past is the caller's job to check, same as the cache path's TTL.
        Task<(string Token, DateTimeOffset ExpiresAtUtc)?> DeviceSessionGetAsync(string apiId);

        /// Persists the device's latest DetectSensors command result (JSON) plus when it was reported - null resultJson clears a stale/malformed result rather than leaving a previous scan's stale data displayed.
        Task DeviceSensorDetectionResultSetAsync(int deviceID, string? resultJson, DateTimeOffset detectedAt);

        /// Generates a new random AES-256 key for LoRa private-protocol uplink encryption, stores it, resets every deviceLoRaSession row to gone (a fresh key restarts replay tracking), and returns the raw hex - the ONLY time it's ever returned, the admin must copy it into the node's own provisioning now.
        Task<string> DeviceLoRaPrivateKeyGenerateAsync(int deviceID);

        /// Replay check - true (accepted) for a never-seen (bootNonce, counter) pair or a counter higher than this bootNonce's own high-water mark, false (replay) otherwise; keeps only the 32 most-recently-started sessions per device so a crash-looping node can't grow this table unbounded.
        Task<bool> DeviceLoRaSessionAcceptAsync(int deviceID, byte[] bootNonce, uint counter);

        /// The device's current (highest LastSeenUtc) v2 session, for Web Device Details' operator-visible "LoRa v2 session" card - null if the device has never sent an accepted uplink.
        Task<DeviceLoRaSessionInfo?> DeviceLoRaLatestSessionGetAsync(int deviceID);

        /// Fleet status for every device in the tenant, or everywhere when tenantID is null (caller must check CallerReadsDevicesGlobally first) - Online comes from DeviceFleetStatus.ComputeOnline.
        Task<IList<DeviceFleetStatus>> DeviceFleetGetAsync(int? tenantID);

        /// Same shape as one DeviceFleetGetAsync row, scoped to a single device - null if deviceID doesn't exist or (when tenantID is set) belongs to another tenant.
        Task<DeviceFleetStatus?> DeviceFleetStatusGetAsync(int deviceID, int? tenantID);

        /// Drops the cached DeviceFleetGetAsync snapshot (own-tenant and global) - called by IDeviceFarmUnitRepository after any write that changes a device's fleet row (e.g. zone assignment).
        Task InvalidateFleetCacheAsync(int? tenantID);

        // Device events

        /// Skips (returns false) an identical eventType for the same device within ServerConfig.EventDedupeMinutes - a flapping "NoInternet" every loop cycle must not flood the table.
        Task<bool> EventDevicePushAsync(int deviceID, int tenantID, DeviceEventType eventType, string? message);

        /// Most recent events for one device, newest first, capped at <paramref name="limit"/> - tenantID is the caller's own, so a cross-tenant device just matches zero rows rather than leaking events.
        Task<IList<DeviceEvent>> EventDeviceGetAsync(int? deviceID, int? tenantID, int limit = 100);

        /// Marks one event acknowledged so it stops counting toward Unit/Zone Orange status - returns false rather than throwing when the id doesn't match (wrong tenant or already gone).
        Task<bool> EventDeviceAcknowledgeAsync(int idEventDevice, int? tenantID);

        // Offline alert background worker

        /// Every enabled device across every tenant - the worker is not tenant-scoped, it runs once for the whole install.
        Task<IList<OfflineAlertCandidate>> OfflineAlertCandidatesGetAsync();

        /// Sets (or clears, notifiedAt: null) OfflineNotifiedAt on one device's diagnostic row.
        Task DeviceOfflineNotifiedSetAsync(int deviceID, DateTimeOffset? notifiedAt);

        // Low-battery alert background worker

        /// Every enabled device across every tenant with its latest battery reading - not tenant-scoped, runs once for the whole install.
        Task<IList<LowBatteryAlertCandidate>> LowBatteryAlertCandidatesGetAsync();

        /// Sets (or clears, notifiedAt: null) LowBatteryNotifiedAt on one device's diagnostic row.
        Task DeviceLowBatteryNotifiedSetAsync(int deviceID, DateTimeOffset? notifiedAt);

        // Frost alert background worker

        /// Latest Temperature+Humidity per enabled device in one tenant - frost forecasting is per-tenant, so local confirmation must be too.
        Task<IList<FrostSensorReading>> FrostSensorReadingsGetAsync(int tenantId);

        // Simulation Mode (per-metric overrides on an existing physical device)

        /// Null when the device has never had a Simulation row created (equivalent to Enabled=false, every field null) - callers should treat both the same way.
        Task<DeviceSimulation?> DeviceSimulationGetAsync(int deviceID);

        /// Upsert - creates the row on first call for a device, updates every field afterward.
        Task DeviceSimulationSetAsync(int deviceID, DeviceSimulation value);
    }
}
