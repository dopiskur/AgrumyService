using Agrumy.Shared;
using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Rules;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Controllers.API
{
    public partial class DeviceFarmUnitApiController
    {
        /// Every Zone within one Unit - ownership is checked on the Unit, not per-zone, since a zone always belongs to exactly one unit.
        [Authorize]
        [HttpGet("Zone")]
        public async Task<ActionResult<IList<DeviceFarmUnitZone>>> DeviceFarmUnitZonesGet(int? idDeviceFarmUnit)
        {
            var (unit, error) = await EnsureOwnedUnitAsync(idDeviceFarmUnit, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.DeviceFarmUnitZonesGetAsync(unit!.IDDeviceFarmUnit!.Value));
        }

        /// Single zone by id, so a caller that needs to patch one field can fetch-then-resubmit the whole object - DeviceFarmUnitZoneUpdateAsync overwrites unconditionally, it does not merge.
        [Authorize]
        [HttpGet("ZoneById")]
        public async Task<ActionResult<DeviceFarmUnitZone>> DeviceFarmUnitZoneGetById(int? idDeviceFarmUnitZone)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: false);
            return error ?? Ok(zone);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone")]
        public async Task<ActionResult<DeviceFarmUnitZone>> DeviceFarmUnitZoneAdd([FromBody] DeviceFarmUnitZone zone)
        {
            var (unit, error) = await EnsureOwnedUnitAsync(zone.DeviceFarmUnitID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            zone.TenantID = unit!.TenantID; // the owning unit's organization, not necessarily the caller's (a Global admin may add to another organization's unit)
            DeviceFarmUnitZone added;
            try
            {
                added = await deviceFarmUnitRepo.DeviceFarmUnitZoneAddAsync(zone, () => quotaEnforcer.CheckCanAddZoneAsync(zone.TenantID));
            }
            catch (QuotaLimitExceededException ex)
            {
                return ForbidWith(ex.Message);
            }
            await WriteAuditAsync("DeviceFarmUnitZone.Created", added.TenantID, "DeviceFarmUnitZone", added.IDDeviceFarmUnitZone.ToString()!, added.DeviceFarmUnitZoneName);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Zone")]
        public async Task<ActionResult<bool>> DeviceFarmUnitZoneUpdate([FromBody] DeviceFarmUnitZone zone)
        {
            var (existing, error) = await EnsureOwnedZoneAsync(zone.IDDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }

            // Catches human error server-side - see Agrumy.Api.Utils.SafetyLimitValidation for the shared range.
            if (!SafetyLimitValidation.IsValid(zone.WaterPumpMaxRunSeconds))
            {
                return BadRequest($"WaterPump max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(zone.WaterPumpCooldownSeconds))
            {
                return BadRequest($"WaterPump cooldown must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(zone.HeatingMaxRunSeconds))
            {
                return BadRequest($"Heating max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(zone.VentilationMaxRunSeconds))
            {
                return BadRequest($"Ventilation max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }

            zone.TenantID = existing!.TenantID; // payload cannot move a zone to another organization
            zone.DeviceFarmUnitID = existing.DeviceFarmUnitID; // ...or to another unit - rename only, see DeviceFarmUnitZoneMigrate for the deliberate version
            await deviceFarmUnitRepo.DeviceFarmUnitZoneUpdateAsync(zone);
            await WriteAuditAsync("DeviceFarmUnitZone.Updated", existing.TenantID, "DeviceFarmUnitZone", existing.IDDeviceFarmUnitZone.ToString()!, zone.DeviceFarmUnitZoneName);
            return true;
        }

        /// Deliberate unit reassignment, kept out of DeviceFarmUnitZoneUpdate's payload on purpose (see the comment there); both ends' ownership are checked separately since a Global admin can own zone and target unit in different organizations.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Zone/{idDeviceFarmUnitZone}/Migrate")]
        public async Task<ActionResult<bool>> DeviceFarmUnitZoneMigrate(int idDeviceFarmUnitZone, int idTargetDeviceFarmUnit)
        {
            var (zone, zoneError) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (zoneError != null)
            {
                return zoneError;
            }
            var (targetUnit, unitError) = await EnsureOwnedUnitAsync(idTargetDeviceFarmUnit, forWrite: true);
            if (unitError != null)
            {
                return unitError;
            }
            if (targetUnit!.TenantID != zone!.TenantID)
            {
                return BadRequest("Target unit belongs to a different tenant.");
            }
            if (targetUnit.IDDeviceFarmUnit == zone.DeviceFarmUnitID)
            {
                return BadRequest("Zone is already in that unit.");
            }

            string fromUnitName = (await deviceFarmUnitRepo.DeviceFarmUnitGetByIdAsync(zone.DeviceFarmUnitID))?.DeviceFarmUnitName ?? zone.DeviceFarmUnitID.ToString();
            await deviceFarmUnitRepo.DeviceFarmUnitZoneMigrateAsync(zone.IDDeviceFarmUnitZone!.Value, targetUnit.IDDeviceFarmUnit!.Value);
            await WriteAuditAsync("DeviceFarmUnitZone.Migrated", zone.TenantID, "DeviceFarmUnitZone", zone.IDDeviceFarmUnitZone.ToString()!, $"{zone.DeviceFarmUnitZoneName}: {fromUnitName} -> {targetUnit.DeviceFarmUnitName}");
            return true;
        }

        // A Text widget's own Label carries its content, so it's the one type that's never optional; every other type's Label just overrides an auto-generated title.
        private const int MaxWidgetsPerZone = 20;

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Zone/{idDeviceFarmUnitZone}/Widgets")]
        public async Task<ActionResult<bool>> DeviceFarmUnitZoneWidgetsSet(int idDeviceFarmUnitZone, [FromBody] List<DashboardWidget> widgets)
        {
            var (existing, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (widgets.Count > MaxWidgetsPerZone)
            {
                return BadRequest($"At most {MaxWidgetsPerZone} widgets per zone.");
            }
            if (widgets.Any(w => w.Type == DashboardWidgetType.Text && string.IsNullOrWhiteSpace(w.Label)))
            {
                return BadRequest("A text widget needs a label.");
            }

            // Each SensorValue/SensorTrend widget carries its OWN (level, levelId) target, independent of the zone whose page it's displayed on, so its ownership is checked separately here rather than inherited from the EnsureOwnedZoneAsync check above.
            foreach (DashboardWidget w in widgets)
            {
                if (w.Type == DashboardWidgetType.SensorValue || w.Type == DashboardWidgetType.SensorTrend)
                {
                    if (w.AggregationLevel is not HierarchyNodeKind level || w.LevelID is not int levelId)
                    {
                        return BadRequest("A sensor widget needs an aggregation level and target.");
                    }
                    if (await EnsureOwnedAggregationTargetAsync(level, levelId, forWrite: false) != null)
                    {
                        return BadRequest("A sensor widget references a farm/unit/zone you don't have access to.");
                    }
                }
                else if (w.Type == DashboardWidgetType.RelayStatus)
                {
                    if (w.LevelID is not int relayZoneId || (await EnsureOwnedZoneAsync(relayZoneId, forWrite: false)).Error != null)
                    {
                        return BadRequest("A relay status widget needs a zone you have access to.");
                    }
                }
                else if (w.Type == DashboardWidgetType.AlertStatus)
                {
                    if (w.AlertEventType is not NotificationEventType alertType || !AlertStatusEventTypes.Contains(alertType))
                    {
                        return BadRequest("An alert status widget needs an alert type with a live status.");
                    }
                    if (w.AggregationLevel is not (HierarchyNodeKind.Farm or HierarchyNodeKind.Unit or HierarchyNodeKind.Zone) || w.LevelID is not int alertLevelId
                        || await EnsureOwnedAggregationTargetAsync(w.AggregationLevel.Value, alertLevelId, forWrite: false) != null)
                    {
                        return BadRequest("An alert status widget needs a Farm/Unit/Zone you have access to.");
                    }
                }
            }

            await deviceFarmUnitRepo.DeviceFarmUnitZoneWidgetsSetAsync(idDeviceFarmUnitZone, widgets);
            await WriteAuditAsync("DeviceFarmUnitZone.WidgetsUpdated", existing!.TenantID, "DeviceFarmUnitZone", idDeviceFarmUnitZone.ToString(), $"{widgets.Count} widget(s)");
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Zone/{idDeviceFarmUnitZone}/GridColumns")]
        public async Task<ActionResult<bool>> DeviceFarmUnitZoneGridColumnsSet(int idDeviceFarmUnitZone, [FromBody] int columns)
        {
            var (_, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (columns is < 1 or > 6)
            {
                return BadRequest("Grid columns must be between 1 and 6.");
            }
            await deviceFarmUnitRepo.DeviceFarmUnitZoneGridColumnsSetAsync(idDeviceFarmUnitZone, columns);
            return true;
        }

        /// NotificationEventType values with a live, continuously-queryable "currently active" signal - see EfDeviceFarmUnitRepository.DashboardAlertStatusGetAsync. RuleTriggered fires on transition only (no "still true" state) and the three Satellite* types are organization-quota concerns, not farm/unit/zone-scoped, so none of them belong on this widget.
        private static readonly NotificationEventType[] AlertStatusEventTypes =
        [
            NotificationEventType.Offline, NotificationEventType.LowBattery, NotificationEventType.TankRefill, NotificationEventType.Frost,
        ];

        /// One widget's own (level, levelId) scope, same ownership reasoning as DashboardWidgetAggregateGet above.
        [Authorize]
        [HttpGet("Dashboard/AlertStatus")]
        public async Task<ActionResult<DashboardAlertStatus>> DashboardAlertStatusGet(NotificationEventType eventType, HierarchyNodeKind level, int levelId)
        {
            if (!AlertStatusEventTypes.Contains(eventType))
            {
                return BadRequest("This alert type has no live status to show.");
            }
            if (level is not (HierarchyNodeKind.Farm or HierarchyNodeKind.Unit or HierarchyNodeKind.Zone))
            {
                return BadRequest("Alert status is only scoped to Farm/Unit/Zone.");
            }
            ActionResult? error = await EnsureOwnedAggregationTargetAsync(level, levelId, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(new DashboardAlertStatus { IsActive = await deviceFarmUnitRepo.DashboardAlertStatusGetAsync(level, levelId, eventType) });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Zone")]
        public async Task<ActionResult<bool>> DeviceFarmUnitZoneDelete(int? idDeviceFarmUnitZone)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await deviceFarmUnitRepo.DeviceFarmUnitZoneDeleteAsync(zone!.IDDeviceFarmUnitZone!.Value);
            await WriteAuditAsync("DeviceFarmUnitZone.Deleted", zone.TenantID, "DeviceFarmUnitZone", idDeviceFarmUnitZone.ToString()!, zone.DeviceFarmUnitZoneName);
            return true;
        }

        /// Devices with no current zone, filtered to controller- or sensor-capable - the "Add Controller"/"Add Sensor" picker list.
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("Unassigned")]
        public async Task<ActionResult<IList<DeviceDto>>> DeviceUnassignedGet(bool controllerCapable) =>
            Ok((await deviceFarmUnitRepo.DeviceUnassignedGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId, controllerCapable))
                .Select(d => d.ToDto()).ToList());

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Assign")]
        public async Task<ActionResult<bool>> DeviceAssign([FromBody] DeviceZoneAssignment body)
        {
            var (device, deviceError) = await EnsureOwnedDeviceAsync(
                () => deviceRepo.DeviceGetByIdAsync(body.IDDevice), "Device", forWrite: true);
            if (deviceError != null)
            {
                return deviceError;
            }

            var (zone, zoneError) = await EnsureOwnedZoneAsync(body.IDDeviceFarmUnitZone, forWrite: true);
            if (zoneError != null)
            {
                return zoneError;
            }

            // Unconditional, no exception for a caller who legitimately crosses organizations for the two ownership checks above - a device must never end up assigned into another organization's zone, not even by a Global admin's mistake.
            if (device!.TenantID != zone!.TenantID)
            {
                return ForbidWith("Device and zone belong to different tenants.");
            }

            // A zone has at most one controller (not required, but capped at one). This is only a fast-path rejection for the common case - DeviceAssignToZoneAsync re-checks it inside its own transaction, which is what actually closes the race between two concurrent assigns.
            if (device!.DeviceControllerEnabled == true && await deviceFarmUnitRepo.DeviceFarmUnitZoneHasControllerAsync(body.IDDeviceFarmUnitZone))
            {
                return Conflict("This zone already has a controller assigned.");
            }

            if (!await deviceFarmUnitRepo.DeviceAssignToZoneAsync(body.IDDevice, body.IDDeviceFarmUnitZone, enforceOneControllerPerZone: true))
            {
                return Conflict("This zone already has a controller assigned.");
            }
            await WriteAuditAsync("Device.AssignedToZone", device.TenantID, "Device", body.IDDevice.ToString(), $"zone {body.IDDeviceFarmUnitZone}");
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Unassign")]
        public async Task<ActionResult<bool>> DeviceUnassign(int? idDevice)
        {
            var (device, error) = await EnsureOwnedDeviceAsync(
                () => deviceRepo.DeviceGetByIdAsync(idDevice), "Device", forWrite: true);
            if (error != null)
            {
                return error;
            }
            await deviceFarmUnitRepo.DeviceUnassignFromZoneAsync(device!.IDDevice!.Value);
            await WriteAuditAsync("Device.UnassignedFromZone", device.TenantID, "Device", idDevice.ToString()!, null);
            return true;
        }
    }
}
