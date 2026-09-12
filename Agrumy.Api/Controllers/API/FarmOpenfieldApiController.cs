using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Api.Satellite;
using Agrumy.Api.Storage;
using Agrumy.Shared.Geo;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Open-Field's Sowing/FarmParcel/FarmParcelZone CRUD, device assignment, and Farm-with-extension creation (restructure R) - the Open-Field mirror of DeviceFarmUnitApiController's Unit/Zone CRUD. Farm-level CRUD/reorder/delete/recycle-bin stays on DeviceFarmUnitApiController (shared by both branches); this controller only owns what's genuinely new.
    [Route("/api/FarmOpenfield")]
    public class FarmOpenfieldApiController(IFarmOpenfieldRepository farmOpenfieldRepo, ISowingRepository sowingRepo, IFarmParcelRepository farmParcelRepo, IFieldLogRepository fieldLogRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IDeviceRepository deviceRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, Agrumy.Api.Quota.TenantQuotaEnforcer quotaEnforcer, Agrumy.Api.Commands.ManualActuateService manualActuate, ISatelliteSceneRepository satelliteSceneRepo, ISatelliteImagerySourceFactory satelliteSourceFactory, SatelliteStorage satelliteStorage, Agrumy.Api.BackgroundWorkers.BackgroundJobQueue jobQueue, ISatelliteConfigRepository satelliteConfigRepo) : ApiControllerBase(userRepo, auditLogRepo, cache)
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
                return ForbidWith(ex.Message);
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
                return ForbidWith(ex.Message);
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
            // D13 - karenca doesn't block Harvest hard, but an unexpired one needs an explicit, audited confirmation.
            DateOnly? earliestHarvestDate = await fieldLogRepo.EarliestHarvestDateAsync(request.IDSowing);
            bool beforePhi = earliestHarvestDate is DateOnly ehd && DateOnly.FromDateTime(DateTime.UtcNow) < ehd;
            if (beforePhi && !request.Confirm)
            {
                return Conflict(new { earliestHarvestDate });
            }
            if (beforePhi)
            {
                int daysEarly = earliestHarvestDate!.Value.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
                await WriteAuditAsync("FieldLog.HarvestBeforePhi", sowing!.TenantID, "Sowing", request.IDSowing.ToString(), $"{daysEarly} day(s) before EarliestHarvestDate");
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

        #region Dnevnik (fieldLogEntry/fieldLogAttachment, D6/D7/D13)

        [Authorize]
        [HttpGet("Sowing/FieldLog")]
        public async Task<ActionResult<IList<FieldLogEntry>>> FieldLogEntriesGet(int idSowing)
        {
            var (sowing, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await fieldLogRepo.FieldLogEntriesGetAsync(sowing!.IDSowing, null, null, null));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Sowing/FieldLog")]
        public async Task<ActionResult<FieldLogEntry>> FieldLogEntryAdd([FromBody] FieldLogEntry entry)
        {
            if (entry.SowingID is not int idSowing)
            {
                return BadRequest("SowingID is required.");
            }
            var (sowing, error) = await EnsureOwnedCropAsync(idSowing, forWrite: true);
            if (error != null)
            {
                return error;
            }
            entry.TenantID = sowing!.TenantID;
            FieldLogEntry added = await fieldLogRepo.FieldLogEntryAddAsync(entry);
            await WriteAuditAsync("FieldLog.EntryAdded", sowing.TenantID, "Sowing", idSowing.ToString(), entry.EntryType.ToString());
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Sowing/FieldLog")]
        public async Task<ActionResult<bool>> FieldLogEntryDelete(int idFieldLogEntry)
        {
            FieldLogEntry? entry = await fieldLogRepo.FieldLogEntryGetByIdAsync(idFieldLogEntry);
            if (entry?.SowingID is not int idSowing)
            {
                return NotFound();
            }
            var (sowing, error) = await EnsureOwnedCropAsync(idSowing, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await fieldLogRepo.FieldLogEntryDeleteAsync(idFieldLogEntry);
            await WriteAuditAsync("FieldLog.EntryDeleted", sowing!.TenantID, "Sowing", idSowing.ToString(), entry.EntryType.ToString());
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Sowing/FieldLog/{idFieldLogEntry}/Attachment")]
        [RequestSizeLimit(20_000_000)]
        public async Task<ActionResult<FieldLogAttachment>> FieldLogAttachmentAdd(int idFieldLogEntry, IFormFile file, [FromServices] Agrumy.Api.Storage.FieldLogAttachmentStorage storage)
        {
            FieldLogEntry? entry = await fieldLogRepo.FieldLogEntryGetByIdAsync(idFieldLogEntry);
            if (entry?.SowingID is not int idSowing)
            {
                return NotFound();
            }
            var (_, error) = await EnsureOwnedCropAsync(idSowing, forWrite: true);
            if (error != null)
            {
                return error;
            }
            string extension = Path.GetExtension(file.FileName);
            await using Stream stream = file.OpenReadStream();
            (string storedName, long sizeBytes) = await storage.SaveAsync(stream, extension);
            FieldLogAttachment added = await fieldLogRepo.FieldLogAttachmentAddAsync(new FieldLogAttachment
            {
                FieldLogEntryID = idFieldLogEntry,
                FileName = file.FileName,
                ContentType = file.ContentType,
                StoragePath = storedName,
                SizeBytes = sizeBytes,
            });
            return Ok(added);
        }

        [Authorize]
        [HttpGet("Sowing/FieldLog/{idFieldLogEntry}/Attachments")]
        public async Task<ActionResult<IList<FieldLogAttachment>>> FieldLogAttachmentsGet(int idFieldLogEntry) =>
            Ok(await fieldLogRepo.FieldLogAttachmentsGetAsync(idFieldLogEntry));

        [Authorize]
        [HttpGet("Sowing/FieldLog/Attachment/{idFieldLogAttachment}/Download")]
        public async Task<ActionResult> FieldLogAttachmentDownload(int idFieldLogAttachment, [FromServices] Agrumy.Api.Storage.FieldLogAttachmentStorage storage)
        {
            FieldLogAttachment? attachment = await fieldLogRepo.FieldLogAttachmentGetByIdAsync(idFieldLogAttachment);
            if (attachment?.FieldLogEntryID is not int idFieldLogEntry)
            {
                return NotFound();
            }
            FieldLogEntry? entry = await fieldLogRepo.FieldLogEntryGetByIdAsync(idFieldLogEntry);
            if (entry?.SowingID is not int idSowing)
            {
                return NotFound();
            }
            var (_, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            string path = storage.PathFor(attachment.StoragePath!);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }
            return PhysicalFile(path, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
        }

        /// D13 - shown on Sowing Details so "Harvest" can warn before the caller even tries.
        [Authorize]
        [HttpGet("Sowing/EarliestHarvestDate")]
        public async Task<ActionResult<DateOnly?>> EarliestHarvestDateGet(int idSowing)
        {
            var (_, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await fieldLogRepo.EarliestHarvestDateAsync(idSowing));
        }

        /// "Bilanca N" - kg N per ha across every fertilization entry, with the tenant's warning threshold (default 170) so the Web page can flag it without a second round trip.
        [Authorize]
        [HttpGet("Sowing/NitrogenBalance")]
        public async Task<ActionResult<double?>> NitrogenBalanceGet(int idSowing)
        {
            var (_, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await fieldLogRepo.NitrogenBalanceKgPerHaAsync(idSowing));
        }

        /// Evidencija o uporabi sredstava za zaštitu bilja (SZB) - every PlantProtection entry for the sowing, one CSV row per entry with every legally required field (D7).
        [Authorize]
        [HttpGet("Sowing/{idSowing}/PlantProtectionReport")]
        public async Task<ActionResult> PlantProtectionReportGet(int idSowing)
        {
            var (sowing, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            IList<FieldLogEntry> entries = await fieldLogRepo.FieldLogEntriesGetAsync(idSowing, null, null, null);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Date,Product,ActiveSubstance,Dose,TreatedAreaHa,Reason,PhiDays,Applicator,WeatherConditions");
            foreach (FieldLogEntry entry in entries.Where(e => e.EntryType == EntryType.PlantProtection).OrderBy(e => e.DateUtc))
            {
                PlantProtectionPayload? p = string.IsNullOrEmpty(entry.PayloadJson)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<PlantProtectionPayload>(entry.PayloadJson);
                if (p == null)
                {
                    continue;
                }
                sb.AppendLine(string.Join(",", CsvField(entry.DateUtc.ToString("yyyy-MM-dd")), CsvField(p.ProductName), CsvField(p.ActiveSubstance),
                    CsvField(p.Dose), CsvField(p.TreatedAreaHa.ToString(System.Globalization.CultureInfo.InvariantCulture)), CsvField(p.Reason),
                    CsvField(p.PhiDays.ToString()), CsvField(p.Applicator), CsvField(p.WeatherConditions ?? "")));
            }
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"plant-protection-{sowing!.SowingName}-{idSowing}.csv");
        }

        private static string CsvField(string value) =>
            value.Contains(',') || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

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
                return ForbidWith(ex.Message);
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

        /// S-A - the parcel's outer boundary (Leaflet-Geoman draw/edit on the Web side); validated/normalized by ParcelGeometryValidator before storage.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("FarmParcel/{idFarmParcel}/Geometry")]
        public async Task<ActionResult<ParcelGeometryResult>> FarmParcelGeometrySet(int idFarmParcel, [FromBody] ParcelGeometrySetRequest request)
        {
            var (parcel, error) = await EnsureOwnedFarmParcelAsync(idFarmParcel, forWrite: true);
            if (error != null)
            {
                return error;
            }
            ParcelGeometryResult result;
            try
            {
                result = ParcelGeometryValidator.Validate(request.GeometryGeoJson);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            await farmParcelRepo.FarmParcelGeometrySetAsync(idFarmParcel, result.GeometryGeoJson, result.AreaHectares, result.BboxMinLat, result.BboxMinLon, result.BboxMaxLat, result.BboxMaxLon, request.ArkodParcelId);
            await WriteAuditAsync("FarmParcel.GeometrySet", parcel!.TenantID, "FarmParcel", idFarmParcel.ToString(), $"{result.AreaHectares:0.###} ha");
            return Ok(result);
        }

        [Authorize]
        [HttpGet("FarmParcel/{idFarmParcel}/Geometry")]
        public async Task<ActionResult<FarmParcel>> FarmParcelGeometryGet(int idFarmParcel)
        {
            var (parcel, error) = await EnsureOwnedFarmParcelAsync(idFarmParcel, forWrite: false);
            return error ?? Ok(parcel);
        }

        /// S-A - one zone's subdivision polygon within its parcel's outer boundary; same validator, separate storage so zone separations render independently on the map.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Parcel/{idFarmParcelZone}/Geometry")]
        public async Task<ActionResult<ParcelGeometryResult>> ParcelZoneGeometrySet(int idFarmParcelZone, [FromBody] ParcelGeometrySetRequest request)
        {
            var (zone, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            ParcelGeometryResult result;
            try
            {
                result = ParcelGeometryValidator.Validate(request.GeometryGeoJson);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            await farmParcelRepo.FarmParcelZoneGeometrySetAsync(idFarmParcelZone, result.GeometryGeoJson, result.AreaHectares, result.BboxMinLat, result.BboxMinLon, result.BboxMaxLat, result.BboxMaxLon);
            await WriteAuditAsync("FarmParcelZone.GeometrySet", zone!.TenantID, "FarmParcelZone", idFarmParcelZone.ToString(), $"{result.AreaHectares:0.###} ha");
            return Ok(result);
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
                return ForbidWith("Device and parcel belong to different tenants.");
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

        // Same cap as DeviceFarmUnitApiController.MaxWidgetsPerZone.
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
                else if (w.Type == DashboardWidgetType.AlertStatus)
                {
                    if (w.AlertEventType is not NotificationEventType alertType || !AlertStatusEventTypes.Contains(alertType))
                    {
                        return BadRequest("An alert status widget needs an alert type with a live status.");
                    }
                    if (w.AggregationLevel is not (HierarchyNodeKind.Farm or HierarchyNodeKind.Unit or HierarchyNodeKind.Zone) || w.LevelID is not int alertLevelId
                        || await EnsureOwnedAggregationTargetAsync(w.AggregationLevel.Value, alertLevelId) != null)
                    {
                        return BadRequest("An alert status widget needs a Farm/Unit/Zone you have access to.");
                    }
                }
            }

            await farmParcelRepo.FarmParcelZoneWidgetsSetAsync(idFarmParcelZone, widgets);
            await WriteAuditAsync("FarmParcelZone.WidgetsUpdated", existing!.TenantID, "FarmParcelZone", idFarmParcelZone.ToString(), $"{widgets.Count} widget(s)");
            return true;
        }

        // Same alert types as DeviceFarmUnitApiController.AlertStatusEventTypes (RuleTriggered/Satellite* have no continuous "currently active" state).
        private static readonly NotificationEventType[] AlertStatusEventTypes =
        [
            NotificationEventType.Offline, NotificationEventType.LowBattery, NotificationEventType.TankRefill, NotificationEventType.Frost,
        ];

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Parcel/{idFarmParcelZone}/GridColumns")]
        public async Task<ActionResult<bool>> ParcelGridColumnsSet(int idFarmParcelZone, [FromBody] int columns)
        {
            var (_, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (columns is < 1 or > 6)
            {
                return BadRequest("Grid columns must be between 1 and 6.");
            }
            await farmParcelRepo.FarmParcelZoneGridColumnsSetAsync(idFarmParcelZone, columns);
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

        #region Satellite (Detaljni dizajn S, B4) - reader roles allowed, tenant-scoped via EnsureOwnedParcelAsync same as everything else on this zone

        [Authorize]
        [HttpGet("Parcel/{idFarmParcelZone}/Satellite/Scenes")]
        public async Task<ActionResult<IList<FarmParcelZoneSatelliteScene>>> SatelliteScenesGet(int idFarmParcelZone)
        {
            var (_, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await satelliteSceneRepo.ScenesGetAsync(idFarmParcelZone));
        }

        /// D10 - serves the cached PNG if one is on disk; otherwise renders it now from the stored grid (scalar indices) or re-fetches+renders from the provider (composites, which never store a grid) and caches the result before returning. A backfilled-but-never-viewed scalar scene has no grid yet either - that one costs a real Process API call, same "first click pays once" behavior as a composite.
        [Authorize]
        [HttpGet("Parcel/{idFarmParcelZone}/Satellite/Scenes/{idScene}/Index/{index}")]
        public async Task<ActionResult> SatelliteIndexPngGet(int idFarmParcelZone, int idScene, SatelliteIndex index)
        {
            var (zone, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            IList<FarmParcelZoneSatelliteScene> scenes = await satelliteSceneRepo.ScenesGetAsync(idFarmParcelZone);
            FarmParcelZoneSatelliteScene? scene = scenes.FirstOrDefault(s => s.IDFarmParcelZoneSatelliteScene == idScene);
            if (scene == null)
            {
                return NotFound();
            }

            string path = satelliteStorage.PathFor(zone!.TenantID ?? 0, idFarmParcelZone, scene.SceneDateUtc, index);
            byte[]? cached = satelliteStorage.TryRead(path);
            if (cached != null)
            {
                return File(cached, "image/png");
            }

            ParcelSatelliteIndex? indexRow = await satelliteSceneRepo.IndexGetAsync(idScene, index);
            bool isScalar = Evalscripts.All[index].HasStatistics;
            byte[] png;
            if (isScalar && indexRow?.GridBase64 is string gridB64)
            {
                png = SatellitePaletteRenderer.RenderPng(Convert.FromBase64String(gridB64), index);
            }
            else
            {
                ISatelliteImagerySource? source = await satelliteSourceFactory.ForAsync(zone.TenantID ?? 0, HttpContext.RequestAborted);
                if (source == null || zone.GeometryGeoJson == null)
                {
                    return StatusCode(503, "Satellite provider not configured, or this zone has no boundary.");
                }
                TenantSatelliteConfig? satConfig = await satelliteConfigRepo.SatelliteConfigGetAsync(zone.TenantID ?? 0);
                IndexRender rendered = await source.RenderIndexAsync(zone.TenantID ?? 0, satConfig?.Collection ?? SatelliteCollection.Sentinel2, satConfig?.CommercialCollectionId, new SceneCandidate(scene.SourceSceneId, scene.SceneDateUtc, scene.CloudPercent), zone.GeometryGeoJson, index, renderPng: true, renderGrid: isScalar, HttpContext.RequestAborted);
                if (isScalar && rendered.GridRaw != null)
                {
                    await satelliteSceneRepo.IndexUpsertAsync(new ParcelSatelliteIndex { SceneID = idScene, Index = index, GridBase64 = Convert.ToBase64String(rendered.GridRaw), BoundsJson = rendered.BoundsJson, StatsJson = rendered.StatsJson });
                    png = SatellitePaletteRenderer.RenderPng(rendered.GridRaw, index);
                }
                else if (rendered.PngBytes != null)
                {
                    png = rendered.PngBytes;
                }
                else
                {
                    return StatusCode(502, "Provider did not return a usable image.");
                }
            }

            await satelliteStorage.SaveAsync(path, png, HttpContext.RequestAborted);
            return File(png, "image/png");
        }

        [Authorize]
        [HttpGet("Parcel/{idFarmParcelZone}/Satellite/Series")]
        public async Task<ActionResult<IList<SatelliteSeriesPoint>>> SatelliteSeriesGet(int idFarmParcelZone, SatelliteIndex index, DateOnly? from, DateOnly? to, bool onlyReliable = true)
        {
            var (_, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await satelliteSceneRepo.SeriesGetAsync(idFarmParcelZone, index, from, to, onlyReliable));
        }

        private const int MaxMoistureSeriesWindowDays = 400;

        /// The sensor-side of the Zone-tab dual-axis chart, aligned by day against SatelliteSeriesGet's SceneDateUtc.
        [Authorize]
        [HttpGet("Parcel/{idFarmParcelZone}/Satellite/MoistureSeries")]
        public async Task<ActionResult<IList<FarmParcelZoneMoistureSeriesPoint>>> MoistureSeriesGet(int idFarmParcelZone, DateOnly from, DateOnly to)
        {
            var (_, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            if (to < from || (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).TotalDays > MaxMoistureSeriesWindowDays)
            {
                return BadRequest($"Invalid window: 'to' must be on or after 'from', and the span must not exceed {MaxMoistureSeriesWindowDays} days.");
            }
            return Ok(await farmParcelRepo.FarmParcelZoneMoistureSeriesGetAsync(idFarmParcelZone, from, to));
        }

        #endregion

        #region Satellite four-level map (Detaljni dizajn S, sesija C) - one partial, scope only changes which zones are drawn

        [Authorize]
        [HttpGet("{scope}/{id}/Satellite")]
        public async Task<ActionResult<SatelliteMapResponse>> SatelliteMapGet(SatelliteMapScope scope, int id, SatelliteIndex index, DateOnly? date = null)
        {
            var (zones, parcels, error) = await ResolveSatelliteScopeAsync(scope, id, forWrite: false);
            if (error != null)
            {
                return error;
            }

            DateOnly targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var zoneEntries = new List<SatelliteMapZoneEntry>();
            foreach (FarmParcelZone zone in zones)
            {
                int idZone = zone.IDFarmParcelZone!.Value;
                FarmParcelZoneSatelliteScene? scene = await satelliteSceneRepo.SceneAtOrBeforeDateAsync(idZone, targetDate);
                if (scene == null)
                {
                    // D4 - the zone is still drawn (its outline, no raster) rather than disappearing from the map.
                    zoneEntries.Add(new SatelliteMapZoneEntry { ZoneId = idZone, ZoneName = zone.FarmParcelZoneName, ParcelId = zone.FarmParcelID, GeometryGeoJson = zone.GeometryGeoJson, HasData = false });
                    continue;
                }
                ParcelSatelliteIndex? indexRow = await satelliteSceneRepo.IndexGetAsync(scene.IDFarmParcelZoneSatelliteScene, index);
                zoneEntries.Add(new SatelliteMapZoneEntry
                {
                    ZoneId = idZone,
                    ZoneName = zone.FarmParcelZoneName,
                    ParcelId = zone.FarmParcelID,
                    GeometryGeoJson = zone.GeometryGeoJson,
                    HasData = indexRow != null,
                    SceneDateUtc = scene.SceneDateUtc,
                    Reliable = scene.Reliable,
                    SceneId = indexRow == null ? null : scene.IDFarmParcelZoneSatelliteScene,
                    StatsJson = indexRow?.StatsJson,
                });
            }

            var parcelEntries = parcels.Select(p => new SatelliteMapParcelEntry { ParcelId = p.IDFarmParcel!.Value, ParcelName = p.FarmParcelName, GeometryGeoJson = p.GeometryGeoJson }).ToList();
            return Ok(new SatelliteMapResponse { Zones = zoneEntries, Parcels = parcelEntries });
        }

        [Authorize]
        [HttpGet("{scope}/{id}/Satellite/Dates")]
        public async Task<ActionResult<IList<DateOnly>>> SatelliteMapDatesGet(SatelliteMapScope scope, int id)
        {
            var (zones, _, error) = await ResolveSatelliteScopeAsync(scope, id, forWrite: false);
            if (error != null)
            {
                return error;
            }
            List<int> zoneIds = zones.Select(z => z.IDFarmParcelZone!.Value).ToList();
            return Ok(await satelliteSceneRepo.DistinctSceneDatesAsync(zoneIds));
        }

        /// D-manager only; rate-limited to one enqueue per tenant per 5 minutes (ICache-backed cooldown) so a repeatedly-clicked button can't flood the job queue. Re-syncs the whole tenant (the daily job's own per-tenant loop), not just the clicked scope - a fully zone-scoped sync would need SatelliteSyncEvaluator split into a per-zone entry point, deferred as a fast-follow rather than duplicating its backfill/incremental logic here.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("{scope}/{id}/Satellite/SyncNow")]
        public async Task<ActionResult> SatelliteMapSyncNow(SatelliteMapScope scope, int id)
        {
            var (_, _, error) = await ResolveSatelliteScopeAsync(scope, id, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (CallerTenantId is not int tenantId)
            {
                return ForbidWith("Caller has no tenant.");
            }
            string cooldownKey = $"satellite-syncnow-cooldown:{tenantId}";
            if (await Cache.GetAsync<string>(cooldownKey) != null)
            {
                return StatusCode(429, "A sync was already requested for this tenant in the last 5 minutes.");
            }
            await Cache.SetAsync(cooldownKey, "1", TimeSpan.FromMinutes(5));
            jobQueue.Enqueue((services, ct) => services.GetRequiredService<Agrumy.Api.BackgroundWorkers.SatelliteSyncEvaluator>().RunOnceAsync(ct));
            await WriteAuditAsync("Satellite.SyncNowRequested", tenantId, scope.ToString(), id.ToString(), null);
            return Ok();
        }

        #endregion

        private async Task<(IList<FarmParcelZone> Zones, IList<FarmParcel> Parcels, ActionResult? Error)> ResolveSatelliteScopeAsync(SatelliteMapScope scope, int id, bool forWrite)
        {
            switch (scope)
            {
                case SatelliteMapScope.Farm:
                {
                    var (_, error) = await EnsureOwnedFarmAsync(id, forWrite);
                    if (error != null)
                    {
                        return ([], [], error);
                    }
                    FarmOpenfield? openfield = await farmOpenfieldRepo.FarmOpenfieldGetByFarmIdAsync(id);
                    if (openfield?.IDFarmOpenfield is not int idOpenfield)
                    {
                        return ([], [], NotFound());
                    }
                    IList<FarmParcel> parcels = await farmParcelRepo.FarmParcelsGetAsync(idOpenfield);
                    var zones = new List<FarmParcelZone>();
                    foreach (FarmParcel p in parcels)
                    {
                        zones.AddRange(await farmParcelRepo.FarmParcelZonesGetAsync(p.IDFarmParcel!.Value));
                    }
                    return (zones, parcels, null);
                }
                case SatelliteMapScope.Sowing:
                {
                    var (_, error) = await EnsureOwnedCropAsync(id, forWrite);
                    if (error != null)
                    {
                        return ([], [], error);
                    }
                    IList<FarmParcelZone> zones = await sowingRepo.SowingOccupiedZonesGetAsync(id);
                    var parcels = new List<FarmParcel>();
                    foreach (int idParcel in zones.Select(z => z.FarmParcelID).Distinct())
                    {
                        if (await farmParcelRepo.FarmParcelGetByIdAsync(idParcel) is FarmParcel parcel)
                        {
                            parcels.Add(parcel);
                        }
                    }
                    return (zones, parcels, null);
                }
                case SatelliteMapScope.Parcel:
                {
                    var (parcel, error) = await EnsureOwnedFarmParcelAsync(id, forWrite);
                    if (error != null)
                    {
                        return ([], [], error);
                    }
                    IList<FarmParcelZone> zones = await farmParcelRepo.FarmParcelZonesGetAsync(id);
                    return (zones, [parcel!], null);
                }
                case SatelliteMapScope.Zone:
                {
                    var (zone, error) = await EnsureOwnedParcelAsync(id, forWrite);
                    if (error != null)
                    {
                        return ([], [], error);
                    }
                    FarmParcel? parcel = await farmParcelRepo.FarmParcelGetByIdAsync(zone!.FarmParcelID);
                    return ([zone], parcel == null ? [] : [parcel], null);
                }
                default:
                    return ([], [], BadRequest("Unknown scope."));
            }
        }

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
