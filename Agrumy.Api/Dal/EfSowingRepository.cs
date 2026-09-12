using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// ISowingRepository - see its own doc comment. Needs IDeviceOutboxRepository/IDeviceRepository for the same ConfigVersion-bump/outbox-signal/fleet-cache-invalidate triple every other assignment-mutating facet uses (ParcelMigrateAsync, DeviceAssignToParcelAsync, ...).
    internal sealed class EfSowingRepository(AgrumyDbContext db, IDeviceRepository deviceRepository, IDeviceOutboxRepository outboxRepository) : ISowingRepository
    {
        public async Task<IList<Sowing>> SowingsGetAsync(int? tenantID)
        {
            IQueryable<SowingRow> q = db.Sowings.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(s => s.TenantID == tenantID);
            }
            var rows = await q.OrderByDescending(s => s.StartDate).ToListAsync();
            return await ToDtosAsync(rows);
        }

        public async Task<Sowing?> SowingGetByIdAsync(int idSowing)
        {
            var row = await db.Sowings.AsNoTracking().FirstOrDefaultAsync(s => s.IDSowing == idSowing);
            return row == null ? null : (await ToDtosAsync([row])).Single();
        }

        public Task<Sowing> SowingAddAsync(Sowing sowing, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                int cropID = sowing.CropID != 0
                    ? sowing.CropID
                    : await FindOrCreateCropAsync(sowing.TenantID, sowing.SowingName ?? "Sowing");
                var row = new SowingRow
                {
                    TenantID = sowing.TenantID,
                    FarmID = sowing.FarmID,
                    CropID = cropID,
                    Variety = sowing.Variety,
                    SeedRateKgPerHa = sowing.SeedRateKgPerHa,
                    StartDate = sowing.StartDate == default ? DateOnly.FromDateTime(DateTime.UtcNow) : sowing.StartDate,
                    ExpectedDurationDays = sowing.ExpectedDurationDays > 0 ? sowing.ExpectedDurationDays : 90,
                    Status = (int)GrowingCycleStatus.Planned,
                    Notes = sowing.Notes,
                };
                db.Sowings.Add(row);
                await db.SaveChangesAsync();
                return (await ToDtosAsync([row])).Single();
            });

        public async Task SowingUpdateAsync(Sowing sowing)
        {
            var row = await db.Sowings.FirstOrDefaultAsync(s => s.IDSowing == sowing.IDSowing);
            if (row == null)
            {
                return;
            }
            row.Variety = sowing.Variety;
            row.SeedRateKgPerHa = sowing.SeedRateKgPerHa;
            row.ExpectedDurationDays = sowing.ExpectedDurationDays;
            row.Notes = sowing.Notes;
            await db.SaveChangesAsync();
        }

        public async Task SowingStartAsync(int idSowing, IReadOnlyList<int> farmParcelZoneIds, Func<Task<string?>>? quotaCheckAsync = null)
        {
            if (farmParcelZoneIds.Count == 0)
            {
                return;
            }
            await QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                var zones = await db.FarmParcelZones.Where(z => farmParcelZoneIds.Contains(z.IDFarmParcelZone)).ToListAsync();
                if (zones.Any(z => z.CurrentSowingID != null))
                {
                    throw new InvalidOperationException("One or more zones already have an active sowing.");
                }
                DateTimeOffset now = DateTimeOffset.UtcNow;
                foreach (FarmParcelZoneRow zone in zones)
                {
                    zone.CurrentSowingID = idSowing;
                    db.SowingFarmParcelZones.Add(new SowingFarmParcelZoneRow { SowingID = idSowing, FarmParcelZoneID = zone.IDFarmParcelZone, AssignedUtc = now });
                }
                await db.Sowings.Where(s => s.IDSowing == idSowing).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, (int)GrowingCycleStatus.Active));
                await db.SaveChangesAsync();
                return true;
            });
            await SyncDevicesAsync(farmParcelZoneIds, idSowing);
        }

        public async Task SowingCloseAsync(int idSowing, int? closedByUserID)
        {
            var occupied = await db.SowingFarmParcelZones.Where(l => l.SowingID == idSowing && l.ReleasedUtc == null).ToListAsync();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            List<int> zoneIds = occupied.Select(l => l.FarmParcelZoneID).ToList();
            foreach (var link in occupied)
            {
                link.ReleasedUtc = now;
            }
            if (zoneIds.Count > 0)
            {
                await db.FarmParcelZones.Where(z => zoneIds.Contains(z.IDFarmParcelZone))
                    .ExecuteUpdateAsync(set => set.SetProperty(z => z.CurrentSowingID, (int?)null));
            }
            await db.Sowings.Where(s => s.IDSowing == idSowing)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, (int)GrowingCycleStatus.Closed)
                    .SetProperty(s => s.ClosedUtc, now)
                    .SetProperty(s => s.ClosedByUserID, closedByUserID));
            await db.SaveChangesAsync();
            await SyncDevicesAsync(zoneIds, null);
        }

        public async Task SowingDeleteAsync(int idSowing) =>
            await db.Sowings.Where(s => s.IDSowing == idSowing).ExecuteDeleteAsync();

        public async Task<Sowing> SowingRestoreAsync(Sowing sowing, IReadOnlyList<int> occupiedFarmParcelZoneIds)
        {
            var row = new SowingRow
            {
                TenantID = sowing.TenantID,
                FarmID = sowing.FarmID,
                CropID = sowing.CropID,
                Variety = sowing.Variety,
                SeedRateKgPerHa = sowing.SeedRateKgPerHa,
                StartDate = sowing.StartDate,
                ExpectedDurationDays = sowing.ExpectedDurationDays,
                Status = (int)sowing.Status,
                HarvestDate = sowing.HarvestDate,
                ClosedUtc = sowing.ClosedUtc,
                Notes = sowing.Notes,
            };
            db.Sowings.Add(row);
            await db.SaveChangesAsync();
            if (occupiedFarmParcelZoneIds.Count > 0)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                await db.FarmParcelZones.Where(z => occupiedFarmParcelZoneIds.Contains(z.IDFarmParcelZone))
                    .ExecuteUpdateAsync(set => set.SetProperty(z => z.CurrentSowingID, row.IDSowing));
                foreach (int zoneId in occupiedFarmParcelZoneIds)
                {
                    db.SowingFarmParcelZones.Add(new SowingFarmParcelZoneRow { SowingID = row.IDSowing, FarmParcelZoneID = zoneId, AssignedUtc = now });
                }
                await db.SaveChangesAsync();
            }
            return (await ToDtosAsync([row])).Single();
        }

        public async Task<IList<FarmParcelZone>> SowingOccupiedZonesGetAsync(int idSowing)
        {
            var zoneIds = await db.SowingFarmParcelZones.AsNoTracking()
                .Where(l => l.SowingID == idSowing && l.ReleasedUtc == null)
                .Select(l => l.FarmParcelZoneID)
                .ToListAsync();
            var rows = await db.FarmParcelZones.AsNoTracking().Where(z => zoneIds.Contains(z.IDFarmParcelZone)).ToListAsync();
            return rows.Select(EfFarmParcelRepository.ToDtoZone).ToList();
        }

        public async Task<(SensorAverages Averages, SensorTrend Trend)> SowingAggregateAsync(int idSowing)
        {
            var snapshots = await db.Devices.AsNoTracking().Where(d => d.SowingID == idSowing)
                .Select(d => new
                {
                    LatestSensorDataId = db.SensorData.AsNoTracking().Where(s => s.DeviceID == d.IDDevice).OrderByDescending(s => s.DateCreated).Select(s => (int?)s.IDSensorData).FirstOrDefault(),
                })
                .ToListAsync();
            var ids = snapshots.Where(s => s.LatestSensorDataId != null).Select(s => s.LatestSensorDataId!.Value).ToList();
            var readings = ids.Count == 0 ? [] : await db.SensorData.AsNoTracking().Where(s => ids.Contains(s.IDSensorData)).ToListAsync();
            return (new SensorAverages
            {
                Temperature = readings.Select(r => r.Temperature).Average(),
                Humidity = readings.Select(r => r.Humidity).Average(),
                Moisture = readings.Select(r => r.Moisture).Average(),
            }, new SensorTrend());
        }

        public async Task<IList<SowingDashboard>> SowingDashboardGetAsync(int? tenantID)
        {
            IQueryable<SowingRow> q = db.Sowings.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(s => s.TenantID == tenantID);
            }
            var rows = await q.OrderByDescending(s => s.StartDate).ToListAsync();
            var dtos = await ToDtosAsync(rows);
            var result = new List<SowingDashboard>();
            foreach ((SowingRow row, Sowing dto) in rows.Zip(dtos))
            {
                int zoneCount = await db.SowingFarmParcelZones.AsNoTracking().CountAsync(l => l.SowingID == row.IDSowing && l.ReleasedUtc == null);
                int deviceCount = await db.Devices.AsNoTracking().CountAsync(d => d.SowingID == row.IDSowing);
                (SensorAverages averages, SensorTrend trend) = await SowingAggregateAsync(row.IDSowing);
                result.Add(new SowingDashboard
                {
                    IDSowing = row.IDSowing,
                    SowingName = dto.SowingName,
                    FarmID = row.FarmID,
                    ZoneCount = zoneCount,
                    DeviceCount = deviceCount,
                    Averages = averages,
                    Trend = trend,
                    Status = ZoneStatus.Green,
                });
            }
            return result;
        }

        private async Task SyncDevicesAsync(IReadOnlyList<int> farmParcelZoneIds, int? sowingID)
        {
            if (farmParcelZoneIds.Count == 0)
            {
                return;
            }
            var deviceIds = await db.Devices.AsNoTracking().Where(d => farmParcelZoneIds.Contains(d.FarmParcelZoneID!.Value)).Select(d => d.IDDevice).ToListAsync();
            if (deviceIds.Count == 0)
            {
                return;
            }
            await db.Devices.Where(d => deviceIds.Contains(d.IDDevice))
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.SowingID, sowingID).SetProperty(d => d.ConfigVersion, d => (d.ConfigVersion ?? 0) + 1));
            foreach (int deviceId in deviceIds)
            {
                await outboxRepository.AddOutboxItemAsync(deviceId, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            }
            int? tenantID = await db.Sowings.AsNoTracking().Where(s => s.IDSowing == (sowingID ?? 0)).Select(s => (int?)s.TenantID).FirstOrDefaultAsync();
            await deviceRepository.InvalidateFleetCacheAsync(tenantID);
        }

        private async Task<int> FindOrCreateCropAsync(int? tenantID, string name)
        {
            int? existing = await db.Crops.AsNoTracking()
                .Where(c => c.Name == name && (c.TenantID == null || c.TenantID == tenantID))
                .OrderByDescending(c => c.TenantID)
                .Select(c => (int?)c.IDCrop)
                .FirstOrDefaultAsync();
            if (existing is int id)
            {
                return id;
            }
            var row = new CropRow { TenantID = tenantID, Name = name };
            db.Crops.Add(row);
            await db.SaveChangesAsync();
            return row.IDCrop;
        }

        private async Task<List<Sowing>> ToDtosAsync(IReadOnlyList<SowingRow> rows)
        {
            var cropIds = rows.Select(r => r.CropID).Distinct().ToList();
            var cropNames = await db.Crops.AsNoTracking().Where(c => cropIds.Contains(c.IDCrop)).ToDictionaryAsync(c => c.IDCrop, c => c.Name);
            return rows.Select(row =>
            {
                string? cropName = cropNames.GetValueOrDefault(row.CropID);
                return new Sowing
                {
                    IDSowing = row.IDSowing,
                    TenantID = row.TenantID,
                    FarmID = row.FarmID,
                    CropID = row.CropID,
                    Variety = row.Variety,
                    SeedRateKgPerHa = row.SeedRateKgPerHa,
                    StartDate = row.StartDate,
                    ExpectedDurationDays = row.ExpectedDurationDays,
                    Status = (GrowingCycleStatus)row.Status,
                    HarvestDate = row.HarvestDate,
                    ClosedUtc = row.ClosedUtc,
                    ClosedByUserID = row.ClosedByUserID,
                    Notes = row.Notes,
                    SowingName = string.IsNullOrEmpty(row.Variety) ? cropName : $"{cropName} ({row.Variety})",
                };
            }).ToList();
        }
    }
}
