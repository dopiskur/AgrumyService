using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Open-Field's Sowing/FarmParcel/FarmParcelZone CRUD, device assignment, and Farm-with-extension creation (restructure R) - the Open-Field mirror of DeviceFarmUnitApiController's Unit/Zone CRUD. Farm-level CRUD/reorder/delete/recycle-bin stays on DeviceFarmUnitApiController (shared by both branches); this controller only owns what's genuinely new.
    [Route("/api/FarmOpenfield")]
    public class FarmOpenfieldApiController(IFarmOpenfieldRepository farmOpenfieldRepo, ISowingRepository sowingRepo, IFarmParcelRepository farmParcelRepo, IFieldLogRepository fieldLogRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IDeviceRepository deviceRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, Agrumy.Api.Quota.TenantQuotaEnforcer quotaEnforcer, Agrumy.Api.Commands.ManualActuateService manualActuate) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        #region Farm-with-extension creation

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        public async Task<ActionResult<DeviceFarm>> FarmOpenfieldCreate([FromBody] string? farmName)
        {
            (DeviceFarm farm, FarmOpenfield openfield) result;
            try
            {
                result = await farmOpenfieldRepo.FarmOpenfieldCreateAsync(farmName, CallerTenantId, () => quotaEnforcer.CheckCanAddFarmAsync(CallerTenantId));
            }
            catch (QuotaLimitExceededException ex)
            {
                return StatusCode(403, ex.Message);
            }
            await WriteAuditAsync("DeviceFarm.Created", result.farm.TenantID, "DeviceFarm", result.farm.IDDeviceFarm.ToString()!, $"{result.farm.DeviceFarmName} (Open-Field)");
            return Ok(result.farm);
        }

        [Authorize]
        [HttpGet("All")]
        public async Task<ActionResult<IList<FarmOpenfield>>> FarmOpenfieldsGet() =>
            Ok(await farmOpenfieldRepo.FarmOpenfieldsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        /// Every FarmParcel under a farm's Open-Field extension (D2) - the Open-Field page's own parcel list. Zone details come via FarmParcelZonesGet below, one call per parcel, same N+1-is-fine-for-a-small-admin-managed-set reasoning as BuildParcelOptionsAsync elsewhere.
        [Authorize]
        [HttpGet("FarmParcel/All")]
        public async Task<ActionResult<IList<FarmParcel>>> FarmParcelsGet(int idFarmOpenfield)
        {
            var (openfield, farm, error) = await EnsureOwnedOpenfieldByIdAsync(idFarmOpenfield, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await farmParcelRepo.FarmParcelsGetAsync(idFarmOpenfield));
        }

        [Authorize]
        [HttpGet("FarmParcel/{idFarmParcel}/Zones")]
        public async Task<ActionResult<IList<FarmParcelZone>>> FarmParcelZonesGet(int idFarmParcel)
        {
            var (parcel, error) = await EnsureOwnedFarmParcelAsync(idFarmParcel, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await farmParcelRepo.FarmParcelZonesGetAsync(idFarmParcel));
        }

        #endregion

        #region Sowing CRUD ("Crop" wire endpoints - see IApi.cs's own naming note)

        [Authorize]
        [HttpGet("Crop/All")]
        public async Task<ActionResult<IList<Sowing>>> CropsGet() =>
            Ok(await sowingRepo.SowingsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        /// Farm page's Sowing cube grid, same sensor-average/status styling as DeviceFarmUnitApiController.DeviceFarmUnitDashboardGet.
        [Authorize]
        [HttpGet("Crop/Dashboard")]
        public async Task<ActionResult<IList<SowingDashboard>>> CropDashboardGet() =>
            Ok(await sowingRepo.SowingDashboardGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        [Authorize]
        [HttpGet("Crop")]
        public async Task<ActionResult<Sowing>> CropGet(int? idSowing)
        {
            var (crop, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            return error ?? Ok(crop);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Crop")]
        public async Task<ActionResult<Sowing>> CropAdd([FromBody] Sowing crop)
        {
            var (openfield, farm, error) = await EnsureOwnedOpenfieldAsync(crop.FarmID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            crop.TenantID = farm!.TenantID;
            crop.FarmID = farm.IDDeviceFarm!.Value;
            try
            {
                await quotaEnforcer.CheckCanAddCropAsync(crop.TenantID);
            }
            catch (QuotaLimitExceededException ex)
            {
                return StatusCode(403, ex.Message);
            }
            Sowing added = await sowingRepo.SowingAddAsync(crop);
            await WriteAuditAsync("Sowing.Created", added.TenantID, "Sowing", added.IDSowing.ToString()!, added.SowingName);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Crop")]
        public async Task<ActionResult<bool>> CropUpdate([FromBody] Sowing crop)
        {
            var (existing, error) = await EnsureOwnedCropAsync(crop.IDSowing, forWrite: true);
            if (error != null)
            {
                return error;
            }
            crop.TenantID = existing!.TenantID;
            await sowingRepo.SowingUpdateAsync(crop);
            await WriteAuditAsync("Sowing.Updated", existing.TenantID, "Sowing", existing.IDSowing.ToString()!, crop.SowingName);
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Crop")]
        public async Task<ActionResult<bool>> CropDelete(int? idSowing)
        {
            var (crop, error) = await EnsureOwnedCropAsync(idSowing, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await sowingRepo.SowingDeleteAsync(crop!.IDSowing!.Value);
            await WriteAuditAsync("Sowing.Deleted", crop.TenantID, "Sowing", idSowing.ToString()!, crop.SowingName);
            return true;
        }

        /// D9 - Planned -> Active: occupies every listed (must-be-free) zone, writes the sjetva wizard's closing "Finish" step's dnevnik Sowing entry (D6).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Sowing/Start")]
        public async Task<ActionResult<bool>> SowingStart([FromBody] SowingStartRequest request)
        {
            var (sowing, error) = await EnsureOwnedCropAsync(request.IDSowing, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (request.FarmParcelZoneIds is null or { Count: 0 })
            {
                return BadRequest("At least one zone is required to start a sowing.");
            }
            foreach (int idZone in request.FarmParcelZoneIds)
            {
                var (zone, zoneError) = await EnsureOwnedParcelAsync(idZone, forWrite: true);
                if (zoneError != null)
                {
                    return zoneError;
                }
                if (zone!.CurrentSowingID != null)
                {
                    return Conflict($"Zone {idZone} already has an active sowing.");
                }
            }
            try
            {
                await sowingRepo.SowingStartAsync(request.IDSowing, request.FarmParcelZoneIds);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            await fieldLogRepo.FieldLogEntryAddAsync(new FieldLogEntry
            {
                TenantID = sowing!.TenantID,
                SowingID = request.IDSowing,
                EntryType = EntryType.Sowing,
                DateUtc = DateTimeOffset.UtcNow,
                
                Note = $"Started on {request.FarmParcelZoneIds.Count} zone(s).",
            });
            await WriteAuditAsync("Sowing.Started", sowing.TenantID, "Sowing", request.IDSowing.ToString(), string.Join(", ", request.FarmParcelZoneIds));
            return true;
        }

        /// D9 - Active -> Closed: releases every occupied zone, writes the closing Harvest dnevnik entry (IsClosingEntry) plus a harvestResult row (D14 - grouped by default).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Sowing/Close")]
        public async Task<ActionResult<bool>> SowingClose([FromBody] SowingCloseRequest request)
        {
            var (sowing, error) = await EnsureOwnedCropAsync(request.IDSowing, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await sowingRepo.SowingCloseAsync(request.IDSowing, null);
            await fieldLogRepo.HarvestResultAddAsync(new HarvestResult
            {
                SowingID = request.IDSowing,
                DateUtc = DateTimeOffset.UtcNow,
                YieldKg = request.YieldKg,
                MoisturePercent = request.MoisturePercent,
                QualityGrade = request.QualityGrade,
                Note = request.Note,
            });
            await fieldLogRepo.FieldLogEntryAddAsync(new FieldLogEntry
            {
                TenantID = sowing!.TenantID,
                SowingID = request.IDSowing,
                EntryType = EntryType.Harvest,
                DateUtc = DateTimeOffset.UtcNow,
                
                Note = request.Note,
                IsClosingEntry = true,
            });
            await WriteAuditAsync("Sowing.Closed", sowing.TenantID, "Sowing", request.IDSowing.ToString(), $"{request.YieldKg} kg");
            return true;
        }

        #endregion

        #region FarmParcel / FarmParcelZone CRUD ("Parcel" wire endpoints)

        /// The Sowing's currently-occupied zones (D3/D11) - a sowing has no fixed parcel list of its own, occupancy is dynamic.
        [Authorize]
        [HttpGet("Parcel")]
        public async Task<ActionResult<IList<FarmParcelZone>>> ParcelsGet(int? idSowing)
        {
            var (crop, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await sowingRepo.SowingOccupiedZonesGetAsync(crop!.IDSowing!.Value));
        }

        /// Sowing detail page's zone cube grid, same sensor-average/status styling as DeviceFarmUnitApiController.DeviceFarmUnitZoneDashboardListGet.
        [Authorize]
        [HttpGet("Crop/Parcel/Dashboard")]
        public async Task<ActionResult<IList<FarmParcelZoneDashboard>>> ParcelDashboardListGet(int? idSowing)
        {
            var (crop, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            var zones = await sowingRepo.SowingOccupiedZonesGetAsync(crop!.IDSowing!.Value);
            var result = new List<FarmParcelZoneDashboard>();
            foreach (FarmParcelZone zone in zones)
            {
                (SensorAverages averages, SensorTrend trend) = await farmParcelRepo.FarmParcelZoneAggregateAsync(zone.IDFarmParcelZone!.Value);
                result.Add(new FarmParcelZoneDashboard
                {
                    IDFarmParcelZone = zone.IDFarmParcelZone!.Value,
                    IDSowing = zone.CurrentSowingID,
                    FarmParcelZoneName = zone.FarmParcelZoneName,
                    Averages = averages,
                    Trend = trend,
                });
            }
            return Ok(result);
        }

        [Authorize]
        [HttpGet("ParcelById")]
        public async Task<ActionResult<FarmParcelZone>> ParcelGetById(int? idFarmParcelZone)
        {
            var (parcel, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: false);
            return error ?? Ok(parcel);
        }

        /// Creates a FarmParcel (container) under an Open-Field farm - and its first zone, IsWholeParcel=true (D3). Replaces the pre-restructure "add a parcel under a crop" endpoint, which no longer has a matching concept.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("FarmParcel")]
        public async Task<ActionResult<FarmParcelZone>> FarmParcelAdd(int idFarmOpenfield, string farmParcelName)
        {
            var (openfield, farm, error) = await EnsureOwnedOpenfieldByIdAsync(idFarmOpenfield, forWrite: true);
            if (error != null)
            {
                return error;
            }
            try
            {
                await quotaEnforcer.CheckCanAddParcelAsync(farm!.TenantID);
            }
            catch (QuotaLimitExceededException ex)
            {
                return StatusCode(403, ex.Message);
            }
            (FarmParcel parcel, FarmParcelZone zone) = await farmParcelRepo.FarmParcelAddAsync(new FarmParcel { TenantID = farm.TenantID, FarmOpenfieldID = idFarmOpenfield, FarmParcelName = farmParcelName });
            await WriteAuditAsync("FarmParcel.Created", parcel.TenantID, "FarmParcel", parcel.IDFarmParcel.ToString()!, parcel.FarmParcelName);
            return Ok(zone);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Parcel")]
        public async Task<ActionResult<bool>> ParcelUpdate([FromBody] FarmParcelZone parcel)
        {
            var (existing, error) = await EnsureOwnedParcelAsync(parcel.IDFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }

            // Same server-side sanity check as DeviceFarmUnitApiController.DeviceFarmUnitZoneUpdate.
            if (!SafetyLimitValidation.IsValid(parcel.WaterPumpMaxRunSeconds))
            {
                return BadRequest($"WaterPump max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(parcel.WaterPumpCooldownSeconds))
            {
                return BadRequest($"WaterPump cooldown must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(parcel.HeatingMaxRunSeconds))
            {
                return BadRequest($"Heating max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(parcel.VentilationMaxRunSeconds))
            {
                return BadRequest($"Ventilation max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }

            parcel.TenantID = existing!.TenantID;
            await farmParcelRepo.FarmParcelZoneUpdateAsync(parcel);
            await WriteAuditAsync("FarmParcelZone.Updated", existing.TenantID, "FarmParcelZone", existing.IDFarmParcelZone.ToString()!, parcel.FarmParcelZoneName);
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Parcel")]
        public async Task<ActionResult<bool>> ParcelDelete(int? idFarmParcelZone)
        {
            var (parcel, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await farmParcelRepo.FarmParcelZoneDeleteAsync(parcel!.IDFarmParcelZone!.Value);
            await WriteAuditAsync("FarmParcelZone.Deleted", parcel.TenantID, "FarmParcelZone", idFarmParcelZone.ToString()!, parcel.FarmParcelZoneName);
            return true;
        }

        /// D3/D4 - replaces one zone with N named zones; blocked (409, names the sowing) while the source zone has an active sowing.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Parcel/{idFarmParcelZone}/Split")]
        public async Task<ActionResult<IList<FarmParcelZone>>> ParcelSplit(int idFarmParcelZone, [FromBody] List<string> newZoneNames)
        {
            var (zone, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (newZoneNames is null or { Count: < 2 })
            {
                return BadRequest("Split needs at least two new zone names.");
            }
            if (zone!.CurrentSowingID is int idSowing)
            {
                Sowing? holder = await sowingRepo.SowingGetByIdAsync(idSowing);
                return Conflict($"Zone is held by an active sowing ({holder?.SowingName ?? $"#{idSowing}"}) - close it before splitting.");
            }
            IList<FarmParcelZone> created = await farmParcelRepo.FarmParcelZoneSplitAsync(idFarmParcelZone, newZoneNames);
            await WriteAuditAsync("FarmParcelZone.Split", zone.TenantID, "FarmParcelZone", idFarmParcelZone.ToString(), string.Join(", ", newZoneNames));
            return Ok(created);
        }

        /// D3/D4 - merges N zones of the same parcel back into one; blocked while any source zone has an active sowing.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Parcel/Merge")]
        public async Task<ActionResult<FarmParcelZone>> ParcelMerge([FromBody] ParcelMergeRequest request)
        {
            if (request.FarmParcelZoneIds is null or { Count: < 2 })
            {
                return BadRequest("Merge needs at least two zones.");
            }
            int? tenantId = null;
            foreach (int id in request.FarmParcelZoneIds)
            {
                var (zone, zoneError) = await EnsureOwnedParcelAsync(id, forWrite: true);
                if (zoneError != null)
                {
                    return zoneError;
                }
                if (zone!.CurrentSowingID != null)
                {
                    return Conflict($"Zone {id} is held by an active sowing - close it before merging.");
                }
                tenantId ??= zone.TenantID;
            }
            FarmParcelZone merged = await farmParcelRepo.FarmParcelZoneMergeAsync(request.FarmParcelZoneIds, request.MergedName);
            await WriteAuditAsync("FarmParcelZone.Merged", tenantId, "FarmParcelZone", merged.IDFarmParcelZone.ToString()!, string.Join(", ", request.FarmParcelZoneIds));
            return Ok(merged);
        }

        #endregion

        #region Device assignment

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Assign")]
        public async Task<ActionResult<bool>> DeviceAssign([FromBody] DeviceParcelAssignment body)
        {
            var (device, deviceError) = await EnsureOwnedDeviceAsync(() => deviceRepo.DeviceGetByIdAsync(body.IDDevice), "Device", forWrite: true);
            if (deviceError != null)
            {
                return deviceError;
            }
            var (parcel, parcelError) = await EnsureOwnedParcelAsync(body.IDFarmParcelZone, forWrite: true);
            if (parcelError != null)
            {
                return parcelError;
            }
            if (device!.TenantID != parcel!.TenantID)
            {
                return StatusCode(403, "Device and parcel belong to different tenants.");
            }
            if (device.DeviceControllerEnabled == true && await farmParcelRepo.FarmParcelZoneHasControllerAsync(body.IDFarmParcelZone))
            {
                return Conflict("This zone already has a controller assigned.");
            }

            await farmParcelRepo.DeviceAssignToFarmParcelZoneAsync(body.IDDevice, body.IDFarmParcelZone);
            await WriteAuditAsync("Device.AssignedToParcel", device.TenantID, "Device", body.IDDevice.ToString(), $"zone {body.IDFarmParcelZone}");
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Unassign")]
        public async Task<ActionResult<bool>> DeviceUnassign(int? idDevice)
        {
            var (device, error) = await EnsureOwnedDeviceAsync(() => deviceRepo.DeviceGetByIdAsync(idDevice), "Device", forWrite: true);
            if (error != null)
            {
                return error;
            }
            await farmParcelRepo.DeviceUnassignFromFarmParcelZoneAsync(device!.IDDevice!.Value);
            await WriteAuditAsync("Device.UnassignedFromParcel", device.TenantID, "Device", idDevice.ToString()!, null);
            return true;
        }

        #endregion

        #region Manual Actuate - Open-Field's equivalent of DeviceFarmUnitApiController's Zone/ManualActuate

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Parcel/ManualActuate")]
        public async Task<ActionResult<IReadOnlyList<int>>> ParcelManualActuateStart(int idFarmParcelZone, [FromBody] ManualActuateRequest request)
        {
            var (parcel, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            ManualActuateResult result = await manualActuate.StartForParcelAsync(idFarmParcelZone, request);
            if (result.Outcome == ManualActuateOutcome.Success)
            {
                await WriteAuditAsync("FarmParcelZone.ManualActuateStarted", parcel!.TenantID, "FarmParcelZone", idFarmParcelZone.ToString(), $"{request.RelayFunction}/{request.Mode}");
            }
            return result.Outcome switch
            {
                ManualActuateOutcome.Success => Ok(result.AffectedDeviceIds),
                ManualActuateOutcome.TargetNotFound => NotFound(result.Message),
                _ => BadRequest(result.Message),
            };
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Parcel/ManualActuate/Stop")]
        public async Task<ActionResult> ParcelManualActuateStop(int idFarmParcelZone, RelayFunction relayFunction)
        {
            var (parcel, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await manualActuate.StopForParcelAsync(idFarmParcelZone, relayFunction);
            await WriteAuditAsync("FarmParcelZone.ManualActuateStopped", parcel!.TenantID, "FarmParcelZone", idFarmParcelZone.ToString(), relayFunction.ToString());
            return Ok();
        }

        /// The zone's currently-active manual commands (not yet past ExpiresAtUtc) - what the Web UI polls to render "currently active, X remaining".
        [Authorize]
        [HttpGet("Parcel/ManualActuate")]
        public async Task<ActionResult<IList<DeviceManualOverride>>> ParcelManualActuateStatus(int idFarmParcelZone)
        {
            var (_, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            Device? controller = await farmParcelRepo.FarmParcelZoneGetControllerAsync(idFarmParcelZone);
            if (controller?.IDDevice is not int deviceId)
            {
                return Ok(Array.Empty<DeviceManualOverride>());
            }
            return Ok(await deviceFarmUnitRepo.ManualOverridesActiveForDeviceAsync(deviceId));
        }

        #endregion

        #region Dashboard widgets - Open-Field's equivalent of DeviceFarmUnitApiController's Zone/{id}/Widgets

        // Roadmap #238 - same cap as DeviceFarmUnitApiController.MaxWidgetsPerZone.
        private const int MaxWidgetsPerParcel = 20;

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Parcel/{idFarmParcelZone}/Widgets")]
        public async Task<ActionResult<bool>> ParcelWidgetsSet(int idFarmParcelZone, [FromBody] List<DashboardWidget> widgets)
        {
            var (existing, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (widgets.Count > MaxWidgetsPerParcel)
            {
                return BadRequest($"At most {MaxWidgetsPerParcel} widgets per parcel.");
            }
            if (widgets.Any(w => w.Type == DashboardWidgetType.Text && string.IsNullOrWhiteSpace(w.Label)))
            {
                return BadRequest("A text widget needs a label.");
            }

            foreach (DashboardWidget w in widgets)
            {
                if (w.Type == DashboardWidgetType.SensorValue || w.Type == DashboardWidgetType.SensorTrend)
                {
                    if (w.AggregationLevel is not HierarchyNodeKind level || w.LevelID is not int levelId)
                    {
                        return BadRequest("A sensor widget needs an aggregation level and target.");
                    }
                    if (await EnsureOwnedAggregationTargetAsync(level, levelId) != null)
                    {
                        return BadRequest("A sensor widget references a farm/unit/zone/sowing/parcel you don't have access to.");
                    }
                }
                else if (w.Type == DashboardWidgetType.RelayStatus)
                {
                    if (w.LevelID is not int relayZoneId || (await EnsureOwnedZoneAsync(relayZoneId, forWrite: false)).Error != null)
                    {
                        return BadRequest("A relay status widget needs a zone you have access to.");
                    }
                }
            }

            await farmParcelRepo.FarmParcelZoneWidgetsSetAsync(idFarmParcelZone, widgets);
            await WriteAuditAsync("FarmParcelZone.WidgetsUpdated", existing!.TenantID, "FarmParcelZone", idFarmParcelZone.ToString(), $"{widgets.Count} widget(s)");
            return true;
        }

        /// Same dispatch as DeviceFarmUnitApiController's own private helper of the same shape - duplicated rather than shared since the two controllers don't have a common base beyond ApiControllerBase.
        private async Task<ActionResult?> EnsureOwnedAggregationTargetAsync(HierarchyNodeKind level, int levelId) => level switch
        {
            HierarchyNodeKind.Zone => (await EnsureOwnedZoneAsync(levelId, forWrite: false)).Error,
            HierarchyNodeKind.Unit => (await EnsureOwnedUnitAsync(levelId, forWrite: false)).Error,
            HierarchyNodeKind.Farm => (await EnsureOwnedFarmAsync(levelId, forWrite: false)).Error,
            HierarchyNodeKind.FarmParcelZone => (await EnsureOwnedParcelAsync(levelId, forWrite: false)).Error,
            HierarchyNodeKind.Sowing => (await EnsureOwnedCropAsync(levelId, forWrite: false)).Error,
            _ => BadRequest("Unsupported aggregation level."),
        };

        private Task<OwnedResult<DeviceFarmUnitZone>> EnsureOwnedZoneAsync(int idDeviceFarmUnitZone, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmUnitZoneGetByIdAsync(idDeviceFarmUnitZone), z => z.TenantID, "Zone", forWrite);

        private Task<OwnedResult<DeviceFarmUnit>> EnsureOwnedUnitAsync(int idDeviceFarmUnit, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmUnitGetByIdAsync(idDeviceFarmUnit), u => u.TenantID, "Unit", forWrite);

        private Task<OwnedResult<DeviceFarm>> EnsureOwnedFarmAsync(int idDeviceFarm, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmGetByIdAsync(idDeviceFarm), f => f.TenantID, "Farm", forWrite);

        #endregion

        private Task<OwnedResult<Sowing>> EnsureOwnedCropAsync(int? idSowing, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => sowingRepo.SowingGetByIdAsync(idSowing ?? 0), c => c.TenantID, "Crop", forWrite);

        private Task<OwnedResult<FarmParcelZone>> EnsureOwnedParcelAsync(int? idFarmParcelZone, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => farmParcelRepo.FarmParcelZoneGetByIdAsync(idFarmParcelZone ?? 0), p => p.TenantID, "Parcel", forWrite);

        private Task<OwnedResult<FarmParcel>> EnsureOwnedFarmParcelAsync(int idFarmParcel, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => farmParcelRepo.FarmParcelGetByIdAsync(idFarmParcel), p => p.TenantID, "FarmParcel", forWrite);

        private Task<OwnedResult<Device>> EnsureOwnedDeviceAsync(Func<Task<Device?>> lookup, string ownerLabel, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(lookup, d => d.TenantID, ownerLabel, forWrite);

        /// Resolves the owning Farm (for its TenantID) from a FarmID - CropAdd's ownership check is really "does the caller own the Farm this sowing belongs to".
        private async Task<(FarmOpenfield? Openfield, DeviceFarm? Farm, ActionResult? Error)> EnsureOwnedOpenfieldAsync(int idFarm, bool forWrite)
        {
            var (farm, error) = await EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmGetByIdAsync(idFarm), f => f.TenantID, "Farm", forWrite);
            if (error != null)
            {
                return (null, null, error);
            }
            FarmOpenfield? openfield = await farmOpenfieldRepo.FarmOpenfieldGetByFarmIdAsync(idFarm);
            return (openfield, farm, null);
        }

        /// Resolves the owning Farm (for its TenantID) from a FarmOpenfieldID - FarmParcelAdd's ownership check is really "does the caller own the Farm this Open-Field extension belongs to". No direct "get FarmOpenfield by its own id" repository lookup exists (FarmOpenfieldGetByFarmIdAsync is keyed by FarmID), so this scans FarmOpenfieldsGetAsync's small admin-managed set instead of adding a second lookup shape for one caller.
        private async Task<(FarmOpenfield? Openfield, DeviceFarm? Farm, ActionResult? Error)> EnsureOwnedOpenfieldByIdAsync(int idFarmOpenfield, bool forWrite)
        {
            IList<FarmOpenfield> openfields = await farmOpenfieldRepo.FarmOpenfieldsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId);
            FarmOpenfield? openfield = openfields.FirstOrDefault(o => o.IDFarmOpenfield == idFarmOpenfield);
            if (openfield == null)
            {
                return (null, null, NotFound());
            }
            var (farm, error) = await EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmGetByIdAsync(openfield.FarmID), f => f.TenantID, "Farm", forWrite);
            return (openfield, farm, error);
        }
    }
}
