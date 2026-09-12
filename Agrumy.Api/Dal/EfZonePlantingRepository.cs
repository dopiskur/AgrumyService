using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    internal sealed class EfZonePlantingRepository(AgrumyDbContext db) : IZonePlantingRepository
    {
        public async Task<ZonePlanting?> ZonePlantingGetActiveAsync(int idDeviceFarmUnitZone)
        {
            var row = await db.ZonePlantings.AsNoTracking()
                .FirstOrDefaultAsync(z => z.DeviceFarmUnitZoneID == idDeviceFarmUnitZone && z.Status == (int)GrowingCycleStatus.Active);
            return row == null ? null : await ToDtoAsync(row);
        }

        public async Task<IList<ZonePlanting>> ZonePlantingsGetAsync(int idDeviceFarmUnitZone)
        {
            var rows = await db.ZonePlantings.AsNoTracking()
                .Where(z => z.DeviceFarmUnitZoneID == idDeviceFarmUnitZone)
                .OrderByDescending(z => z.PlantedDate)
                .ToListAsync();
            var result = new List<ZonePlanting>();
            foreach (ZonePlantingRow row in rows)
            {
                result.Add(await ToDtoAsync(row));
            }
            return result;
        }

        /// D8 - "jedan aktivan ciklus po zoni", checked here (a friendly, catchable failure) ahead of the DB's own computed-column unique index (ux_zonePlanting_activeZone) which is the real, race-safe guarantee.
        public async Task<ZonePlanting> ZonePlantingStartAsync(ZonePlanting planting)
        {
            if (await ZonePlantingGetActiveAsync(planting.DeviceFarmUnitZoneID) != null)
            {
                throw new InvalidOperationException("This zone already has an active planting cycle.");
            }
            var row = new ZonePlantingRow
            {
                TenantID = planting.TenantID,
                DeviceFarmUnitZoneID = planting.DeviceFarmUnitZoneID,
                CropID = planting.CropID,
                PlantedDate = planting.PlantedDate == default ? DateOnly.FromDateTime(DateTime.UtcNow) : planting.PlantedDate,
                ExpectedDurationDays = planting.ExpectedDurationDays > 0 ? planting.ExpectedDurationDays : 90,
                Status = (int)GrowingCycleStatus.Active,
                Notes = planting.Notes,
            };
            db.ZonePlantings.Add(row);
            await db.SaveChangesAsync();
            return await ToDtoAsync(row);
        }

        public async Task ZonePlantingCloseAsync(int idZonePlanting) =>
            await db.ZonePlantings.Where(z => z.IDZonePlanting == idZonePlanting)
                .ExecuteUpdateAsync(set => set.SetProperty(z => z.Status, (int)GrowingCycleStatus.Closed).SetProperty(z => z.ClosedUtc, DateTimeOffset.UtcNow));

        public async Task<ZonePlanting> ZonePlantingRestoreAsync(ZonePlanting planting)
        {
            var row = new ZonePlantingRow
            {
                TenantID = planting.TenantID,
                DeviceFarmUnitZoneID = planting.DeviceFarmUnitZoneID,
                CropID = planting.CropID,
                PlantedDate = planting.PlantedDate,
                ExpectedDurationDays = planting.ExpectedDurationDays,
                Status = (int)planting.Status,
                HarvestDate = planting.HarvestDate,
                ClosedUtc = planting.ClosedUtc,
                Notes = planting.Notes,
            };
            db.ZonePlantings.Add(row);
            await db.SaveChangesAsync();
            return await ToDtoAsync(row);
        }

        private async Task<ZonePlanting> ToDtoAsync(ZonePlantingRow z)
        {
            string? cropName = await db.Crops.AsNoTracking().Where(c => c.IDCrop == z.CropID).Select(c => c.Name).FirstOrDefaultAsync();
            return new ZonePlanting
            {
                IDZonePlanting = z.IDZonePlanting,
                TenantID = z.TenantID,
                DeviceFarmUnitZoneID = z.DeviceFarmUnitZoneID,
                CropID = z.CropID,
                PlantedDate = z.PlantedDate,
                ExpectedDurationDays = z.ExpectedDurationDays,
                Status = (GrowingCycleStatus)z.Status,
                HarvestDate = z.HarvestDate,
                ClosedUtc = z.ClosedUtc,
                Notes = z.Notes,
                CropName = cropName,
            };
        }
    }
}
