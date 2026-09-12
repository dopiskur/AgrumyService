using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// The minimal shape TankRefillAlertEvaluator needs - WaterLevel is the zone's latest-per-device reading averaged (same shape as SensorAverages.WaterLevel), null when no device in the zone has reported one.
    public sealed record TankRefillAlertCandidate(
        int IDDeviceFarmUnitZone,
        int TenantID,
        string? DeviceFarmUnitZoneName,
        double? WaterLevel,
        int? WaterLevelRawEmpty,
        int? WaterLevelRawFull,
        double? TankCapacityLiters,
        DateTimeOffset? TankRefillNotifiedAt);

    /// Unit/Zone facet of the data layer: CRUD, device assignment, and the hierarchical dashboard aggregation - split out from IDeviceRepository as its own sizeable domain.
    public interface IDeviceFarmUnitRepository
    {
        // ---- Farm CRUD -----------------------------------

        /// Every real Farm in the tenant, or every tenant when tenantID is null (caller must check CallerReadsDevicesGlobally).
        Task<IList<DeviceFarm>> DeviceFarmsGetAsync(int? tenantID);

        /// The Farm with this id (no tenant filter), for ownership checks before an authorized write - or null if none.
        Task<DeviceFarm?> DeviceFarmGetByIdAsync(int? idDeviceFarm);

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        Task<DeviceFarm> DeviceFarmAddAsync(DeviceFarm farm, Func<Task<string?>>? quotaCheckAsync = null);

        /// No-op if the tenant already has any farm.
        Task EnsureFirstFarmAsync(int tenantId);

        Task DeviceFarmUpdateAsync(DeviceFarm farm);

        /// Sets DisplayOrder to each id's index in orderedFarmIds - only touches farms actually owned by tenantID, an id for another tenant (or a stale/unknown id) is silently ignored.
        Task DeviceFarmsReorderAsync(int tenantId, IReadOnlyList<int> orderedFarmIds);

        /// Soft-deletes the Farm AND cascades to every Unit/Zone/Device still attached to it (see AgrumyDbContext's HasQueryFilter on each); a no-op if the id doesn't exist. Use DeviceFarmRecycleBinGetAsync/DeviceFarmRestoreAsync to see/undo it.
        Task DeviceFarmDeleteAsync(int idDeviceFarm);

        /// Every soft-deleted, not-yet-Purged Farm (tenantID null = every tenant) - still visible/restorable in the Recycle Bin listing.
        Task<IList<DeviceFarm>> DeviceFarmRecycleBinGetAsync(int? tenantID);

        /// Ownership-check lookup for a soft-deleted farm (no tenant filter, Purged or not) - null if the farm doesn't exist or isn't deleted.
        Task<DeviceFarm?> DeviceFarmRecycleBinGetByIdAsync(int idDeviceFarm);

        /// Every Deleted AND Purged Farm (tenantID null = every tenant) - marked for permanent removal but still restorable until the purge cycle actually reaps it.
        Task<IList<DeviceFarm>> DeviceFarmPendingPurgeGetAsync(int? tenantID);

        /// Undoes DeviceFarmDeleteAsync's exact cascade (or a pending mark-for-purge) - clears BOTH Deleted and Purged on the farm AND its cascade Units/Zones/Devices; false if the farm doesn't exist, isn't deleted, or belongs to a different tenant.
        Task<bool> DeviceFarmRestoreAsync(int idDeviceFarm, int? tenantID);

        /// Marks an already soft-deleted Farm (and its exact DeviceFarmDeleteAsync cascade of Units/Zones/Devices, matched by DeletedAtUtc) for permanent removal without waiting out its tenant's RecycleBinRetentionDays; still fully restorable via DeviceFarmRestoreAsync until the purge cycle actually runs. False if the farm doesn't exist, isn't deleted, or belongs to a different tenant.
        Task<bool> DeviceFarmRecycleBinMarkPurgedAsync(int idDeviceFarm, int? tenantID);

        /// The automatic half of marking - every Deleted, not-yet-Purged Farm whose OWNING TENANT's effective retention has elapsed. Returns how many were marked.
        Task<int> DeviceFarmRecycleBinMarkPurgedByRetentionAsync(int serverDefaultRetentionDays, CancellationToken ct);

        /// Every farm currently marked Purged, with its owning tenant - the reap step's worklist (DeviceFarmRecycleBinPurgeAsync needs the tenant for its own ownership check).
        Task<IList<(int IDDeviceFarm, int? TenantID)>> DeviceFarmPurgedIdsGetAsync();

        /// The actual, irreversible removal of the farm and its exact cascade (Units/Zones/Devices, matched by DeletedAtUtc) - each device purged the same SensorData-included way as a standalone DeviceRecycleBinPurgeAsync. False if the farm doesn't exist, isn't Deleted+Purged, or belongs to a different tenant.
        Task<bool> DeviceFarmRecycleBinPurgeAsync(int idDeviceFarm, int? tenantID);

        // ---- Unit CRUD -------------------------------------------------

        /// Every Unit in the tenant, or every tenant when tenantID is null (caller must check CallerReadsDevicesGlobally).
        Task<IList<DeviceFarmUnit>> DeviceFarmUnitsGetAsync(int? tenantID);

        /// The Unit with this id (no tenant filter), for ownership checks before an authorized write - same pattern as IDeviceRepository.DeviceGetByIdAsync - or null if none.
        Task<DeviceFarmUnit?> DeviceFarmUnitGetByIdAsync(int? idDeviceFarmUnit);

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        Task<DeviceFarmUnit> DeviceFarmUnitAddAsync(DeviceFarmUnit unit, Func<Task<string?>>? quotaCheckAsync = null);

        Task DeviceFarmUnitUpdateAsync(DeviceFarmUnit unit);

        /// Cascade-deletes every Zone under this Unit first (devices unassigned via DeviceUnassignFromZoneAsync), then the Unit row - a no-op if the id doesn't exist.
        Task DeviceFarmUnitDeleteAsync(int idDeviceFarmUnit);

        /// Sets DisplayOrder to each id's index in orderedUnitIds, scoped to whatever subset the caller drags (one farm's units, or the unassigned bucket) - same convention as DeviceFarmsReorderAsync.
        Task DeviceFarmUnitsReorderAsync(int tenantId, IReadOnlyList<int> orderedUnitIds);

        // ---- Zone CRUD ------------------------------------------------

        /// Every Zone belonging to this Unit.
        Task<IList<DeviceFarmUnitZone>> DeviceFarmUnitZonesGetAsync(int idDeviceFarmUnit);

        /// The Zone with this id (no tenant filter) - for ownership checks - or null if none.
        Task<DeviceFarmUnitZone?> DeviceFarmUnitZoneGetByIdAsync(int? idDeviceFarmUnitZone);

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        Task<DeviceFarmUnitZone> DeviceFarmUnitZoneAddAsync(DeviceFarmUnitZone zone, Func<Task<string?>>? quotaCheckAsync = null);

        Task DeviceFarmUnitZoneUpdateAsync(DeviceFarmUnitZone zone);

        /// The deliberate counterpart to DeviceFarmUnitZoneUpdateAsync, which never touches DeviceFarmUnitID - also moves the zone's own devices' denormalized DeviceFarmUnitID and bumps their ConfigVersion, since their effective Unit-scope rules just changed. False if the zone doesn't exist.
        Task<bool> DeviceFarmUnitZoneMigrateAsync(int idDeviceFarmUnitZone, int idTargetDeviceFarmUnit);

        /// Replaces the zone's whole widget list in one write; saves independently of DeviceFarmUnitZoneUpdateAsync.
        Task DeviceFarmUnitZoneWidgetsSetAsync(int idDeviceFarmUnitZone, List<DashboardWidget> widgets);

        /// Saves independently of DeviceFarmUnitZoneUpdateAsync, same reasoning as DeviceFarmUnitZoneWidgetsSetAsync.
        Task DeviceFarmUnitZoneGridColumnsSetAsync(int idDeviceFarmUnitZone, int columns);

        /// True if the given alert type currently has an active/un-cleared occurrence somewhere within (level, levelId)'s scope; see EfDeviceFarmUnitRepository for which existing per-type state each case reads.
        Task<bool> DashboardAlertStatusGetAsync(HierarchyNodeKind level, int levelId, NotificationEventType eventType);

        /// Unassigns every device currently in this Zone (via DeviceUnassignFromZoneAsync), then deletes the Zone row - a no-op if the id doesn't exist.
        Task DeviceFarmUnitZoneDeleteAsync(int idDeviceFarmUnitZone);

        /// Whether this Zone already has a controller-capable device assigned - a Zone has at most one controller.
        Task<bool> DeviceFarmUnitZoneHasControllerAsync(int idDeviceFarmUnitZone);

        /// The zone's one controller device, or null if none - DeviceOutboxService's Zone-target fan-out errors (does not silently no-op) when this is null.
        Task<Device?> DeviceFarmUnitZoneGetControllerAsync(int idDeviceFarmUnitZone);

        /// Every controller device across every zone under this unit - zones with no controller are simply absent, not an error.
        Task<IList<Device>> DeviceFarmUnitGetControllersAsync(int idDeviceFarmUnit);

        /// Every sensor-only device (DeviceSensorEnabled, not DeviceControllerEnabled) in this zone.
        Task<IList<Device>> DeviceFarmUnitZoneGetSensorsAsync(int idDeviceFarmUnitZone);

        /// Every sensor-only device across every zone under this unit.
        Task<IList<Device>> DeviceFarmUnitGetSensorsAsync(int idDeviceFarmUnit);

        /// Every sensor-only device across every unit/zone under this farm.
        Task<IList<Device>> DeviceFarmGetSensorsAsync(int idDeviceFarm);

        /// Every controller device across every unit/zone under this farm.
        Task<IList<Device>> DeviceFarmGetControllersAsync(int idDeviceFarm);

        /// Every device under this unit regardless of role or zone assignment.
        Task<IList<Device>> DeviceFarmUnitGetDevicesAsync(int idDeviceFarmUnit);

        // ---- Device assignment -----------------------------------------

        /// The "Add Controller"/"Add Sensor" picker list: every unassigned device in the tenant, filtered by DeviceControllerEnabled or DeviceSensorEnabled per controllerCapable.
        Task<IList<Device>> DeviceUnassignedGetAsync(int? tenantID, bool controllerCapable);

        /// Assigns one device to one zone (sets DeviceFarmUnitID from the zone's own, plus DeviceFarmUnitZoneID) and bumps ConfigVersion so the device picks it up on its next poll.
        Task DeviceAssignToZoneAsync(int idDevice, int idDeviceFarmUnitZone);

        /// Resets DeviceFarmUnitID/DeviceFarmUnitZoneID to NULL ("unassigned") - deliberately does NOT bump ConfigVersion or otherwise notify the device.
        Task DeviceUnassignFromZoneAsync(int idDevice);

        // ---- Dashboard aggregation -------------------------------------

        /// One cube per real Unit in scope (tenantID null = every tenant): name, zone/device counts, and the per-sensor-type average across the unit.
        Task<IList<DeviceFarmUnitDashboard>> DeviceFarmUnitDashboardGetAsync(int? tenantID);

        /// One cube per Zone within one Unit, same shape narrowed in scope - Devices list stays empty, populated only by the single-zone detail below.
        Task<IList<DeviceFarmUnitZoneDashboard>> DeviceFarmUnitZoneDashboardListGetAsync(int idDeviceFarmUnit);

        /// Single-zone detail: roll-up plus the actual device list, null if the zone id doesn't exist. ReadCommitted (default) - RuleNotificationEvaluator's only caller, where a since-rolled-back read must never drive a notification decision.
        Task<DeviceFarmUnitZoneDashboard?> DeviceFarmUnitZoneDashboardGetAsync(int idDeviceFarmUnitZone);

        /// Same result shape as DeviceFarmUnitZoneDashboardGetAsync, for the Web dashboard display instead of alert evaluation - ReadUncommitted, same "dirty reads are fine for a display snapshot" reasoning as SensorDataExportGetAsync, so a purge batch never blocks/is blocked by a dashboard load.
        Task<DeviceFarmUnitZoneDashboard?> DeviceFarmUnitZoneDashboardForDisplayGetAsync(int idDeviceFarmUnitZone);

        /// Averages+Trend for one dashboard widget's own (level, levelId) scope - Farm rolls up every zone under every unit of that farm, Unit same narrowed to one unit's zones, Zone is a single zone's own reading.
        Task<DashboardAggregate> DashboardAggregateGetAsync(HierarchyNodeKind level, int levelId);

        // ---- Rules (Zone/Unit/Farm/Global scope) ------------------------------

        /// Every rule scoped to exactly this zone - several rows may share the same RelayFunction/SensorMetric (OR semantics; Relay-action OR is resolved by the firmware, Notification-action OR by RuleNotificationEvaluator).
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForZoneAsync(int idDeviceFarmUnitZone);

        /// Every rule scoped to exactly this unit (Unit scope, not the union of its zones' own rules).
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForUnitAsync(int idDeviceFarmUnit);

        /// Every rule scoped to exactly this farm (Farm scope, not the union of its units'/zones' own rules).
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForFarmAsync(int idDeviceFarm);

        /// Every rule at Global (per-tenant) scope - applies to every farm/unit/zone/crop/parcel the tenant owns unless a more specific scope overrides it for that function/metric.
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForTenantGlobalAsync(int tenantId);

        /// Open-Field's mid-level equivalent of RulesGetForUnitAsync.
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForSowingAsync(int idSowing);

        /// Open-Field's leaf-level equivalent of RulesGetForZoneAsync.
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForFarmParcelZoneAsync(int idFarmParcelZone);

        /// Every rule scoped to exactly this simulation session - evaluated ahead of a member device's real Zone>Unit>Farm>Global rules, falling back to that hierarchy for anything this session has no rule for.
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForSimulationAsync(int idSimulationSession);

        /// Every rule scoped to exactly this experiment - same "evaluated ahead, falls back to the real hierarchy" precedence as RulesGetForSimulationAsync, one tier below it.
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForExperimentAsync(int idExperiment);

        /// One query in place of up to 6 sequential calls to the methods just above - DeviceConfigBuilder resolves every id first, then partitions the flat result back out by each row's own scope FK. A null id means that scope contributes nothing, same as not calling the individual method at all.
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetForHierarchyAsync(int tenantId, int? idSimulationSession, int? idExperiment, int? idZone, int? idFarmParcelZone, int? idUnit, int? idSowing, int? idFarm, bool includeGlobal);

        /// Every Notification-action rule for the tenant across all three real scopes, unresolved (RuleNotificationEvaluator does its own per-zone Zone>Unit>Global resolution) - simulation/experiment-scoped rules excluded, fetched separately per session/experiment.
        Task<IList<DeviceFarmUnitZoneRule>> RulesGetNotificationRulesForTenantAsync(int tenantId);

        /// Single rule by id (no tenant filter) - for ownership checks, resolve its scope then check that scope's tenant - or null if none.
        Task<DeviceFarmUnitZoneRule?> RuleGetByIdAsync(int? idRule);

        Task<int> RuleAddAsync(DeviceFarmUnitZoneRule rule);

        /// Every Notification-action rule in the tenant with a RuleTriggered condition referencing ruleId.
        Task<IList<DeviceFarmUnitZoneRule>> RulesReferencingAsync(int ruleId, int tenantId);

        /// A no-op if the id does not exist. Callers must check RulesReferencingAsync first and refuse to delete a still-referenced rule - this method itself does not guard that.
        Task RuleDeleteAsync(int idRule);

        /// Bumps ConfigVersion for every device assigned to this zone - called after any rules/safety-limit change so the next poll picks it up.
        Task DeviceFarmUnitZoneConfigVersionBumpAsync(int idDeviceFarmUnitZone);

        // ---- Notification rule evaluation state --------------------------

        /// False (not just missing) for a (rule, zone) pair with no row yet.
        Task<bool> RuleNotificationWasTrueGetAsync(int ruleId, int idDeviceFarmUnitZone);

        Task RuleNotificationWasTrueSetAsync(int ruleId, int idDeviceFarmUnitZone, bool wasTrue, DateTime? lastFiredAtUtc);

        // ---- Tank refill alert --------------------------

        /// Every real, tank-calibrated zone (TankCapacityLiters + both raw calibration points set) across every tenant, with its latest averaged WaterLevel reading.
        Task<IList<TankRefillAlertCandidate>> TankRefillAlertCandidatesGetAsync();

        Task TankRefillNotifiedSetAsync(int idDeviceFarmUnitZone, DateTimeOffset? notifiedAt);

        // ---- Manual actuate --------------------------

        /// Upserts on (DeviceID, RelayFunction) - starting a new command for an already-active function replaces it, same "restart the timer" semantics as re-triggering anything else in this system.
        Task ManualOverrideStartAsync(DeviceManualOverride manualOverride);

        /// A no-op if none is active for (deviceId, relayFunction).
        Task ManualOverrideStopAsync(int deviceId, RelayFunction relayFunction);

        /// Every override for this device not yet past ExpiresAtUtc - what DeviceConfigBuilder sends on the next poll and the Web UI shows as "currently active".
        Task<IList<DeviceManualOverride>> ManualOverridesActiveForDeviceAsync(int deviceId);
    }
}
