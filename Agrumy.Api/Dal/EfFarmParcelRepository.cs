using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IFarmParcelRepository - see its own doc comment.
    internal sealed class EfFarmParcelRepository(AgrumyDbContext db, IDeviceRepository deviceRepository, IDeviceOutboxRepository outboxRepository) : IFarmParcelRepository
    {
        public async Task<IList<FarmParcel>> FarmParcelsGetAsync(int idFarmOpenfield)
        {
            var rows = await db.FarmParcels.AsNoTracking().Where(p => p.FarmOpenfieldID == idFarmOpenfield).OrderBy(p => p.FarmParcelName).ToListAsync();
            return rows.Select(ToDtoParcel).ToList();
        }

        public async Task<FarmParcel?> FarmParcelGetByIdAsync(int idFarmParcel)
        {
            var row = await db.FarmParcels.AsNoTracking().FirstOrDefaultAsync(p => p.IDFarmParcel == idFarmParcel);
            return row == null ? null : ToDtoParcel(row);
        }

        public Task<(FarmParcel Parcel, FarmParcelZone Zone)> FarmParcelAddAsync(FarmParcel parcel, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                var row = new FarmParcelRow { TenantID = parcel.TenantID, FarmOpenfieldID = parcel.FarmOpenfieldID, FarmParcelName = parcel.FarmParcelName };
                db.FarmParcels.Add(row);
                await db.SaveChangesAsync();

                var zoneRow = new FarmParcelZoneRow { TenantID = parcel.TenantID, FarmParcelID = row.IDFarmParcel, FarmParcelZoneName = parcel.FarmParcelName, IsWholeParcel = true };
                db.FarmParcelZones.Add(zoneRow);
                await db.SaveChangesAsync();

                return (ToDtoParcel(row), ToDtoZone(zoneRow));
            });

        public async Task FarmParcelUpdateAsync(FarmParcel parcel)
        {
            var row = await db.FarmParcels.FirstOrDefaultAsync(p => p.IDFarmParcel == parcel.IDFarmParcel);
            if (row == null)
            {
                return;
            }
            row.FarmParcelName = parcel.FarmParcelName;
            await db.SaveChangesAsync();
        }

        public async Task FarmParcelGeometrySetAsync(int idFarmParcel, string geometryGeoJson, double areaHectares, double bboxMinLat, double bboxMinLon, double bboxMaxLat, double bboxMaxLon, string? arkodParcelId)
        {
            await db.FarmParcels.Where(p => p.IDFarmParcel == idFarmParcel).ExecuteUpdateAsync(set => set
                .SetProperty(p => p.GeometryGeoJson, geometryGeoJson)
                .SetProperty(p => p.AreaHectares, areaHectares)
                .SetProperty(p => p.BboxMinLat, bboxMinLat)
                .SetProperty(p => p.BboxMinLon, bboxMinLon)
                .SetProperty(p => p.BboxMaxLat, bboxMaxLat)
                .SetProperty(p => p.BboxMaxLon, bboxMaxLon)
                .SetProperty(p => p.ArkodParcelId, arkodParcelId));
            // The whole-parcel zone IS the parcel - it follows the outer boundary, otherwise the zone-driven satellite sync never sees a parcel drawn only at parcel level.
            await db.FarmParcelZones.Where(z => z.FarmParcelID == idFarmParcel && z.IsWholeParcel).ExecuteUpdateAsync(set => set
                .SetProperty(z => z.GeometryGeoJson, geometryGeoJson)
                .SetProperty(z => z.AreaHectares, areaHectares)
                .SetProperty(z => z.BboxMinLat, bboxMinLat)
                .SetProperty(z => z.BboxMinLon, bboxMinLon)
                .SetProperty(z => z.BboxMaxLat, bboxMaxLat)
                .SetProperty(z => z.BboxMaxLon, bboxMaxLon));
        }

        public async Task FarmParcelDeleteAsync(int idFarmParcel)
        {
            var zoneIds = await db.FarmParcelZones.AsNoTracking().Where(z => z.FarmParcelID == idFarmParcel).Select(z => z.IDFarmParcelZone).ToListAsync();
            foreach (int zoneId in zoneIds)
            {
                await FarmParcelZoneDeleteAsync(zoneId);
            }
            await db.FarmParcels.Where(p => p.IDFarmParcel == idFarmParcel).ExecuteDeleteAsync();
        }

        public async Task<IList<FarmParcelZone>> FarmParcelZonesGetAsync(int idFarmParcel)
        {
            var rows = await db.FarmParcelZones.AsNoTracking().Where(z => z.FarmParcelID == idFarmParcel).OrderBy(z => z.FarmParcelZoneName).ToListAsync();
            return rows.Select(ToDtoZone).ToList();
        }

        public async Task<FarmParcelZone?> FarmParcelZoneGetByIdAsync(int idFarmParcelZone)
        {
            var row = await db.FarmParcelZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDFarmParcelZone == idFarmParcelZone);
            return row == null ? null : ToDtoZone(row);
        }

        public async Task FarmParcelZoneUpdateAsync(FarmParcelZone zone)
        {
            var row = await db.FarmParcelZones.FirstOrDefaultAsync(z => z.IDFarmParcelZone == zone.IDFarmParcelZone);
            if (row == null)
            {
                return;
            }
            row.FarmParcelZoneName = zone.FarmParcelZoneName;
            row.WaterPumpMaxRunSeconds = zone.WaterPumpMaxRunSeconds;
            row.WaterPumpCooldownSeconds = zone.WaterPumpCooldownSeconds;
            row.SkipWaterPumpWhenRainPredicted = zone.SkipWaterPumpWhenRainPredicted;
            row.TankCapacityLiters = zone.TankCapacityLiters;
            row.WaterLevelRawEmpty = zone.WaterLevelRawEmpty;
            row.WaterLevelRawFull = zone.WaterLevelRawFull;
            row.WaterPumpMinLevel = zone.WaterPumpMinLevel;
            row.HeatingMaxRunSeconds = zone.HeatingMaxRunSeconds;
            row.VentilationMaxRunSeconds = zone.VentilationMaxRunSeconds;
            row.HeatingFailSafePolicy = (int?)zone.HeatingFailSafePolicy;
            await db.SaveChangesAsync();
            await FarmParcelZoneConfigVersionBumpAsync(row.IDFarmParcelZone);
        }

        public async Task FarmParcelZoneGeometrySetAsync(int idFarmParcelZone, string geometryGeoJson, double areaHectares, double bboxMinLat, double bboxMinLon, double bboxMaxLat, double bboxMaxLon) =>
            await db.FarmParcelZones.Where(z => z.IDFarmParcelZone == idFarmParcelZone).ExecuteUpdateAsync(set => set
                .SetProperty(z => z.GeometryGeoJson, geometryGeoJson)
                .SetProperty(z => z.AreaHectares, areaHectares)
                .SetProperty(z => z.BboxMinLat, bboxMinLat)
                .SetProperty(z => z.BboxMinLon, bboxMinLon)
                .SetProperty(z => z.BboxMaxLat, bboxMaxLat)
                .SetProperty(z => z.BboxMaxLon, bboxMaxLon));

        public async Task<IList<FarmParcelZone>> FarmParcelZoneSplitAsync(int idFarmParcelZone, IReadOnlyList<string> newZoneNames, Func<Task<string?>>? quotaCheckAsync = null)
        {
            List<FarmParcelZoneRow> created = await QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                // AsNoTracking - a stale already-tracked instance (e.g. from SowingStartAsync/SowingCloseAsync's own tracked reads earlier in the same DbContext) would otherwise report a CurrentSowingID that no longer matches the database.
                var source = await db.FarmParcelZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDFarmParcelZone == idFarmParcelZone);
                if (source == null)
                {
                    throw new InvalidOperationException("Zone not found.");
                }
                if (source.CurrentSowingID != null)
                {
                    throw new InvalidOperationException("Cannot split a zone with an active sowing.");
                }
                var rows = new List<FarmParcelZoneRow>();
                foreach (string name in newZoneNames)
                {
                    var row = new FarmParcelZoneRow { TenantID = source.TenantID, FarmParcelID = source.FarmParcelID, FarmParcelZoneName = name, IsWholeParcel = false };
                    db.FarmParcelZones.Add(row);
                    rows.Add(row);
                }
                await db.SaveChangesAsync();
                return rows;
            });
            await FarmParcelZoneDeleteAsync(idFarmParcelZone);
            return created.Select(ToDtoZone).ToList();
        }

        public async Task<FarmParcelZone> FarmParcelZoneMergeAsync(IReadOnlyList<int> farmParcelZoneIds, string mergedName)
        {
            // AsNoTracking - same staleness trap as FarmParcelZoneSplitAsync's own read.
            var sources = await db.FarmParcelZones.AsNoTracking().Where(z => farmParcelZoneIds.Contains(z.IDFarmParcelZone)).ToListAsync();
            if (sources.Count == 0 || sources.Any(z => z.CurrentSowingID != null))
            {
                throw new InvalidOperationException("Cannot merge zones - not found or one has an active sowing.");
            }
            FarmParcelZoneRow first = sources[0];
            var merged = new FarmParcelZoneRow { TenantID = first.TenantID, FarmParcelID = first.FarmParcelID, FarmParcelZoneName = mergedName, IsWholeParcel = sources.Count == 1 };
            db.FarmParcelZones.Add(merged);
            await db.SaveChangesAsync();
            foreach (int id in farmParcelZoneIds)
            {
                await FarmParcelZoneDeleteAsync(id);
            }
            return ToDtoZone(merged);
        }

        public async Task FarmParcelZoneWidgetsSetAsync(int idFarmParcelZone, List<DashboardWidget> widgets)
        {
            var row = await db.FarmParcelZones.FirstOrDefaultAsync(z => z.IDFarmParcelZone == idFarmParcelZone);
            if (row == null)
            {
                return;
            }
            row.DashboardWidgetsJson = System.Text.Json.JsonSerializer.Serialize(widgets, ConditionConfigJson.Options);
            await db.SaveChangesAsync();
        }

        public async Task FarmParcelZoneGridColumnsSetAsync(int idFarmParcelZone, int columns)
        {
            var row = await db.FarmParcelZones.FirstOrDefaultAsync(z => z.IDFarmParcelZone == idFarmParcelZone);
            if (row == null)
            {
                return;
            }
            row.DashboardGridColumns = columns;
            await db.SaveChangesAsync();
        }

        public async Task FarmParcelZoneDeleteAsync(int idFarmParcelZone)
        {
            var deviceIds = await db.Devices.AsNoTracking().Where(d => d.FarmParcelZoneID == idFarmParcelZone).Select(d => d.IDDevice).ToListAsync();
            foreach (int deviceId in deviceIds)
            {
                await DeviceUnassignFromFarmParcelZoneAsync(deviceId);
            }
            var ruleIds = await db.DeviceFarmUnitZoneRules.AsNoTracking().Where(r => r.DeviceFarmParcelZoneID == idFarmParcelZone).Select(r => r.IDDeviceFarmUnitZoneRule).ToListAsync();
            await db.RuleNotificationStates.Where(s => ruleIds.Contains(s.RuleID)).ExecuteDeleteAsync();
            await db.DeviceFarmUnitZoneRules.Where(r => r.DeviceFarmParcelZoneID == idFarmParcelZone).ExecuteDeleteAsync();
            // A closed sowing's occupancy link survives as history (ReleasedUtc set, row kept) - split/merge routinely delete an ex-occupied zone once its sowing is closed, so this history must go with it.
            await db.SowingFarmParcelZones.Where(l => l.FarmParcelZoneID == idFarmParcelZone).ExecuteDeleteAsync();
            await db.FarmParcelZones.Where(z => z.IDFarmParcelZone == idFarmParcelZone).ExecuteDeleteAsync();
        }

        public async Task DeviceAssignToFarmParcelZoneAsync(int idDevice, int idFarmParcelZone)
        {
            var zone = await db.FarmParcelZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDFarmParcelZone == idFarmParcelZone);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == idDevice);
            if (zone == null || device == null)
            {
                return;
            }
            device.FarmParcelZoneID = zone.IDFarmParcelZone;
            device.SowingID = zone.CurrentSowingID;
            // Mutual exclusivity - a device moving onto the Open-Field branch can't still be on the Greenhouse one.
            device.DeviceFarmUnitID = null;
            device.DeviceFarmUnitZoneID = null;
            device.ConfigVersion = (device.ConfigVersion ?? 0) + 1;
            await db.SaveChangesAsync();
            await outboxRepository.AddOutboxItemAsync(idDevice, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            await deviceRepository.InvalidateFleetCacheAsync(device.TenantID);
        }

        public async Task<bool> FarmParcelZoneHasControllerAsync(int idFarmParcelZone) =>
            await db.Devices.AsNoTracking().AnyAsync(d => d.FarmParcelZoneID == idFarmParcelZone && d.DeviceControllerEnabled == true);

        public async Task<Device?> FarmParcelZoneGetControllerAsync(int idFarmParcelZone)
        {
            var row = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.FarmParcelZoneID == idFarmParcelZone && d.DeviceControllerEnabled == true);
            return row == null ? null : EfDeviceRepository.ToDto(row);
        }

        public async Task<IList<Device>> FarmParcelZoneGetSensorsAsync(int idFarmParcelZone)
        {
            var rows = await db.Devices.AsNoTracking().Where(d => d.FarmParcelZoneID == idFarmParcelZone && d.DeviceSensorEnabled == true && d.DeviceControllerEnabled != true).ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        public async Task DeviceUnassignFromFarmParcelZoneAsync(int idDevice)
        {
            int? tenantID = await db.Devices.AsNoTracking().Where(d => d.IDDevice == idDevice).Select(d => (int?)d.TenantID).FirstOrDefaultAsync();
            await db.Devices.Where(d => d.IDDevice == idDevice)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.FarmParcelZoneID, (int?)null).SetProperty(d => d.SowingID, (int?)null));
            await deviceRepository.InvalidateFleetCacheAsync(tenantID);
        }

        public async Task<(SensorAverages Averages, SensorTrend Trend)> FarmParcelZoneAggregateAsync(int idFarmParcelZone)
        {
            var ids = await db.Devices.AsNoTracking().Where(d => d.FarmParcelZoneID == idFarmParcelZone)
                .Select(d => db.SensorData.AsNoTracking().Where(s => s.DeviceID == d.IDDevice).OrderByDescending(s => s.DateCreated).Select(s => (int?)s.IDSensorData).FirstOrDefault())
                .ToListAsync();
            var validIds = ids.Where(i => i != null).Select(i => i!.Value).ToList();
            var readings = validIds.Count == 0 ? [] : await db.SensorData.AsNoTracking().Where(s => validIds.Contains(s.IDSensorData)).ToListAsync();
            return (new SensorAverages
            {
                Temperature = readings.Select(r => r.Temperature).Average(),
                Humidity = readings.Select(r => r.Humidity).Average(),
                Moisture = readings.Select(r => r.Moisture).Average(),
            }, new SensorTrend());
        }

        public async Task<IList<FarmParcelZoneMoistureSeriesPoint>> FarmParcelZoneMoistureSeriesGetAsync(int idFarmParcelZone, DateOnly from, DateOnly to)
        {
            var deviceIds = await db.Devices.AsNoTracking().Where(d => d.FarmParcelZoneID == idFarmParcelZone).Select(d => d.IDDevice).ToListAsync();
            if (deviceIds.Count == 0)
            {
                return [];
            }
            DateTime fromUtc = from.ToDateTime(TimeOnly.MinValue);
            DateTime toUtc = to.ToDateTime(TimeOnly.MaxValue);
            var readings = await db.SensorData.AsNoTracking()
                .Where(s => deviceIds.Contains(s.DeviceID) && s.Moisture != null && s.DateCreated >= fromUtc && s.DateCreated <= toUtc)
                .Select(s => new { s.DateCreated, s.Moisture })
                .ToListAsync();
            return readings
                .GroupBy(r => DateOnly.FromDateTime(r.DateCreated!.Value.UtcDateTime))
                .Select(g => new FarmParcelZoneMoistureSeriesPoint { Date = g.Key, Moisture = g.Average(r => (double)r.Moisture!.Value) })
                .OrderBy(p => p.Date)
                .ToList();
        }

        public async Task<IList<FarmParcelZoneDashboard>> FarmParcelZoneDashboardListGetAsync(int idFarmParcel)
        {
            var rows = await db.FarmParcelZones.AsNoTracking().Where(z => z.FarmParcelID == idFarmParcel).ToListAsync();
            var result = new List<FarmParcelZoneDashboard>();
            foreach (FarmParcelZoneRow row in rows)
            {
                int deviceCount = await db.Devices.AsNoTracking().CountAsync(d => d.FarmParcelZoneID == row.IDFarmParcelZone);
                (SensorAverages averages, SensorTrend trend) = await FarmParcelZoneAggregateAsync(row.IDFarmParcelZone);
                result.Add(new FarmParcelZoneDashboard
                {
                    IDFarmParcelZone = row.IDFarmParcelZone,
                    IDSowing = row.CurrentSowingID,
                    FarmParcelZoneName = row.FarmParcelZoneName,
                    DeviceCount = deviceCount,
                    Averages = averages,
                    Trend = trend,
                    Status = ZoneStatus.Green,
                });
            }
            return result;
        }

        public async Task<IList<FarmParcelZone>> FarmParcelZonesWithGeometryGetAsync(int tenantId)
        {
            var rows = await db.FarmParcelZones.AsNoTracking().Where(z => z.TenantID == tenantId && z.GeometryGeoJson != null).ToListAsync();
            return rows.Select(ToDtoZone).ToList();
        }

        public async Task FarmParcelZoneConfigVersionBumpAsync(int idFarmParcelZone)
        {
            List<int> deviceIds = await db.Devices.AsNoTracking().Where(d => d.FarmParcelZoneID == idFarmParcelZone).Select(d => d.IDDevice).ToListAsync();
            if (deviceIds.Count == 0)
            {
                return;
            }
            await db.Devices.Where(d => deviceIds.Contains(d.IDDevice)).ExecuteUpdateAsync(s => s.SetProperty(d => d.ConfigVersion, d => (d.ConfigVersion ?? 0) + 1));
            foreach (int deviceId in deviceIds)
            {
                await outboxRepository.AddOutboxItemAsync(deviceId, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            }
        }

        private static FarmParcel ToDtoParcel(FarmParcelRow p) => new()
        {
            IDFarmParcel = p.IDFarmParcel,
            TenantID = p.TenantID,
            FarmOpenfieldID = p.FarmOpenfieldID,
            FarmParcelName = p.FarmParcelName,
            GeometryGeoJson = p.GeometryGeoJson,
            AreaHectares = p.AreaHectares,
            BboxMinLat = p.BboxMinLat,
            BboxMinLon = p.BboxMinLon,
            BboxMaxLat = p.BboxMaxLat,
            BboxMaxLon = p.BboxMaxLon,
            ArkodParcelId = p.ArkodParcelId,
            DeletedAtUtc = p.DeletedAtUtc,
        };

        internal static FarmParcelZone ToDtoZone(FarmParcelZoneRow z) => new()
        {
            IDFarmParcelZone = z.IDFarmParcelZone,
            TenantID = z.TenantID,
            FarmParcelID = z.FarmParcelID,
            FarmParcelZoneName = z.FarmParcelZoneName,
            IsWholeParcel = z.IsWholeParcel,
            CurrentSowingID = z.CurrentSowingID,
            WaterPumpMaxRunSeconds = z.WaterPumpMaxRunSeconds,
            WaterPumpCooldownSeconds = z.WaterPumpCooldownSeconds,
            SkipWaterPumpWhenRainPredicted = z.SkipWaterPumpWhenRainPredicted,
            TankCapacityLiters = z.TankCapacityLiters,
            WaterLevelRawEmpty = z.WaterLevelRawEmpty,
            WaterLevelRawFull = z.WaterLevelRawFull,
            WaterPumpMinLevel = z.WaterPumpMinLevel,
            HeatingMaxRunSeconds = z.HeatingMaxRunSeconds,
            VentilationMaxRunSeconds = z.VentilationMaxRunSeconds,
            HeatingFailSafePolicy = (HeatingFailSafePolicyType?)z.HeatingFailSafePolicy,
            DashboardWidgets = string.IsNullOrEmpty(z.DashboardWidgetsJson) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<DashboardWidget>>(z.DashboardWidgetsJson) ?? [],
            DashboardGridColumns = z.DashboardGridColumns,
            GeometryGeoJson = z.GeometryGeoJson,
            AreaHectares = z.AreaHectares,
            BboxMinLat = z.BboxMinLat,
            BboxMinLon = z.BboxMinLon,
            BboxMaxLat = z.BboxMaxLat,
            BboxMaxLon = z.BboxMaxLon,
            SatelliteBackfillCompletedUtc = z.SatelliteBackfillCompletedUtc,
            DeletedAtUtc = z.DeletedAtUtc,
        };
    }
}
