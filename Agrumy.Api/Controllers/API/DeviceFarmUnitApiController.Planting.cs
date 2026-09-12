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
        [Authorize]
        [HttpGet("Zone/{idDeviceFarmUnitZone}/Planting/Active")]
        public async Task<ActionResult<ZonePlanting?>> ZonePlantingActiveGet(int idDeviceFarmUnitZone)
        {
            var (_, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await zonePlantingRepo.ZonePlantingGetActiveAsync(idDeviceFarmUnitZone));
        }

        /// Every cycle the zone has ever had, most recent first - the "usporedba ciklusa po zoni" comparison table.
        [Authorize]
        [HttpGet("Zone/{idDeviceFarmUnitZone}/Planting/History")]
        public async Task<ActionResult<IList<ZonePlanting>>> ZonePlantingHistoryGet(int idDeviceFarmUnitZone)
        {
            var (_, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await zonePlantingRepo.ZonePlantingsGetAsync(idDeviceFarmUnitZone));
        }

        /// "Start planting" (D8) - cropName is looked up/created in the same crop catalog Sowing uses (D12); one active cycle per zone, enforced by EfZonePlantingRepository + the DB's own unique index.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/{idDeviceFarmUnitZone}/Planting/Start")]
        public async Task<ActionResult<ZonePlanting>> ZonePlantingStart(int idDeviceFarmUnitZone, [FromBody] ZonePlantingStartRequest request)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            int idCrop = await cropCatalogRepo.CropFindOrCreateByNameAsync(zone!.TenantID, request.CropName);
            ZonePlanting started;
            try
            {
                started = await zonePlantingRepo.ZonePlantingStartAsync(new ZonePlanting
                {
                    TenantID = zone.TenantID,
                    DeviceFarmUnitZoneID = idDeviceFarmUnitZone,
                    CropID = idCrop,
                    PlantedDate = request.PlantedDate,
                    ExpectedDurationDays = request.ExpectedDurationDays,
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            await fieldLogRepo.FieldLogEntryAddAsync(new FieldLogEntry
            {
                TenantID = zone.TenantID,
                ZonePlantingID = started.IDZonePlanting,
                EntryType = EntryType.Planting,
                DateUtc = DateTimeOffset.UtcNow,
                Note = $"Planted {request.CropName}.",
            });
            await WriteAuditAsync("ZonePlanting.Started", zone.TenantID, "Zone", idDeviceFarmUnitZone.ToString(), request.CropName);
            return Ok(started);
        }

        /// "Close planting" (D8/D13) - same karenca-confirm gate as Sowing's Close.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/{idDeviceFarmUnitZone}/Planting/Close")]
        public async Task<ActionResult<bool>> ZonePlantingClose(int idDeviceFarmUnitZone, [FromBody] ZonePlantingCloseRequest request)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            ZonePlanting? active = await zonePlantingRepo.ZonePlantingGetActiveAsync(idDeviceFarmUnitZone);
            if (active?.IDZonePlanting is not int idZonePlanting)
            {
                return BadRequest("This zone has no active planting cycle.");
            }
            DateOnly? earliestHarvestDate = await fieldLogRepo.EarliestHarvestDateForZonePlantingAsync(idZonePlanting);
            bool beforePhi = earliestHarvestDate is DateOnly ehd && DateOnly.FromDateTime(DateTime.UtcNow) < ehd;
            if (beforePhi && !request.Confirm)
            {
                return Conflict(new { earliestHarvestDate });
            }
            if (beforePhi)
            {
                int daysEarly = earliestHarvestDate!.Value.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
                await WriteAuditAsync("FieldLog.HarvestBeforePhi", zone!.TenantID, "Zone", idDeviceFarmUnitZone.ToString(), $"{daysEarly} day(s) before EarliestHarvestDate");
            }
            await zonePlantingRepo.ZonePlantingCloseAsync(idZonePlanting);
            await fieldLogRepo.HarvestResultAddAsync(new HarvestResult
            {
                ZonePlantingID = idZonePlanting,
                DateUtc = DateTimeOffset.UtcNow,
                YieldKg = request.YieldKg,
                MoisturePercent = request.MoisturePercent,
                QualityGrade = request.QualityGrade,
                Note = request.Note,
            });
            await fieldLogRepo.FieldLogEntryAddAsync(new FieldLogEntry
            {
                TenantID = zone!.TenantID,
                ZonePlantingID = idZonePlanting,
                EntryType = EntryType.Harvest,
                DateUtc = DateTimeOffset.UtcNow,
                Note = request.Note,
                IsClosingEntry = true,
            });
            await WriteAuditAsync("ZonePlanting.Closed", zone.TenantID, "Zone", idDeviceFarmUnitZone.ToString(), $"{request.YieldKg} kg");
            return true;
        }

        [Authorize]
        [HttpGet("Zone/Planting/{idZonePlanting}/FieldLog")]
        public async Task<ActionResult<IList<FieldLogEntry>>> ZonePlantingFieldLogGet(int idZonePlanting) =>
            Ok(await fieldLogRepo.FieldLogEntriesGetAsync(null, null, idZonePlanting, null));

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/Planting/FieldLog")]
        public async Task<ActionResult<FieldLogEntry>> ZonePlantingFieldLogAdd([FromBody] FieldLogEntry entry)
        {
            if (entry.ZonePlantingID is not int idZonePlanting)
            {
                return BadRequest("ZonePlantingID is required.");
            }
            return Ok(await fieldLogRepo.FieldLogEntryAddAsync(entry));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Zone/Planting/FieldLog")]
        public async Task<ActionResult<bool>> ZonePlantingFieldLogDelete(int idFieldLogEntry)
        {
            await fieldLogRepo.FieldLogEntryDeleteAsync(idFieldLogEntry);
            return true;
        }

        [Authorize]
        [HttpGet("Zone/Planting/{idZonePlanting}/EarliestHarvestDate")]
        public async Task<ActionResult<DateOnly?>> ZonePlantingEarliestHarvestDateGet(int idZonePlanting) =>
            Ok(await fieldLogRepo.EarliestHarvestDateForZonePlantingAsync(idZonePlanting));
    }
}
