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
            return row == null ? null : ToDto(row);
        }

        public async Task<IList<ZonePlanting>> ZonePlantingsGetAsync(int idDeviceFarmUnitZone)
        {
            var rows = await db.ZonePlantings.AsNoTracking()
                .Where(z => z.DeviceFarmUnitZoneID == idDeviceFarmUnitZone)
                .OrderByDescending(z => z.PlantedDate)
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<ZonePlanting> ZonePlantingStartAsync(ZonePlanting planting)
        {
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
            return ToDto(row);
        }

        public async Task ZonePlantingCloseAsync(int idZonePlanting) =>
            await db.ZonePlantings.Where(z => z.IDZonePlanting == idZonePlanting)
                .ExecuteUpdateAsync(set => set.SetProperty(z => z.Status, (int)GrowingCycleStatus.Closed).SetProperty(z => z.ClosedUtc, DateTimeOffset.UtcNow));

        private static ZonePlanting ToDto(ZonePlantingRow z) => new()
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
        };
    }
}
