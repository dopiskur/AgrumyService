using Agrumy.Dal;
using Agrumy.Shared;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Dal
{
    /// IFarmOpenfieldRepository - Crop/Parcel CRUD, device assignment, and per-widget dashboard aggregation. Needs IServerConfigRepository (ProblemEvent settings, same as EfDeviceFarmUnitRepository) and IDeviceRepository (fleet-cache invalidation after assign/unassign) - deliberately NOT IDeviceFarmUnitRepository, since that facet in turn needs this one (Crop/Parcel arms of its dashboard-aggregation switch), and a two-way dependency between them isn't resolvable by the DI container.
    internal sealed class EfFarmOpenfieldRepository(AgrumyDbContext db, IOptions<AgrumySettings> settingsOptions, IServerConfigRepository serverConfigRepository, IDeviceRepository deviceRepository) : IFarmOpenfieldRepository
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        /// Same shape as EfDeviceFarmUnitRepository's private UnitZoneDeviceSnapshot - kept as its own copy rather than shared, since the two repositories are otherwise independent (see this class's own doc comment).
        private sealed record CropParcelDeviceSnapshot(
            bool Enabled, bool Online, bool HasRecentProblemEvent,
            double? Temperature, double? SoilTemperature, double? Humidity, int? Moisture, int? Light,
            int? Co2, int? Tvoc, double? Barometer, double? LiquidPH, int? RainLevel, int? WaterLevel, double? Wind,
            double? Ec, double? Weight)
        {
            public double? Vpd => VpdCalculator.Compute(Temperature, Humidity);
            public double? DewPoint => DewPointCalculator.Compute(Temperature, Humidity);
            public double? DewPointSpread => DewPoint is double dp ? Temperature - dp : null;
        }

        private static readonly int[] ProblemEventTypeIds =
        [
            (int)DeviceEventType.AuthFailed,
            (int)DeviceEventType.ConfigSyncFailed,
            (int)DeviceEventType.CrashLoopRollback,
            (int)DeviceEventType.OtaFailed,
            (int)DeviceEventType.Crash,
        ];

        // ---- Farm type-extension row -------------------------------------

        public Task<(DeviceFarm Farm, FarmOpenfield Openfield)> FarmOpenfieldCreateAsync(string? farmName, int? tenantID, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                // Same next-DisplayOrder + insert shape as EfDeviceFarmUnitRepository.DeviceFarmAddAsync - duplicated rather than called into, to avoid a two-way dependency between the two repositories (see this class's own doc comment).
                int nextOrder = await db.DeviceFarms.Where(f => f.TenantID == tenantID).Select(f => (int?)f.DisplayOrder).MaxAsync() ?? -1;
                var farmRow = new DeviceFarmRow { TenantID = tenantID, DeviceFarmName = farmName, FarmType = (int)FarmType.OpenField, DisplayOrder = nextOrder + 1 };
                db.DeviceFarms.Add(farmRow);
                await db.SaveChangesAsync();

                var openfieldRow = new FarmOpenfieldRow { TenantID = tenantID, FarmID = farmRow.IDDeviceFarm };
                db.FarmOpenfields.Add(openfieldRow);
                await db.SaveChangesAsync();

                var farm = new DeviceFarm
                {
                    IDDeviceFarm = farmRow.IDDeviceFarm,
                    TenantID = farmRow.TenantID,
                    DeviceFarmName = farmRow.DeviceFarmName,
                    FarmType = FarmType.OpenField,
                    DisplayOrder = farmRow.DisplayOrder,
                };
                return (farm, ToDtoOpenfield(openfieldRow));
            });

        public async Task<FarmOpenfield?> FarmOpenfieldGetByFarmIdAsync(int idFarm)
        {
            var row = await db.FarmOpenfields.AsNoTracking().FirstOrDefaultAsync(o => o.FarmID == idFarm);
            return row == null ? null : ToDtoOpenfield(row);
        }

        public async Task<IList<FarmOpenfield>> FarmOpenfieldsGetAsync(int? tenantID)
        {
            IQueryable<FarmOpenfieldRow> q = db.FarmOpenfields.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(o => o.TenantID == tenantID);
            }
            return (await q.ToListAsync()).Select(ToDtoOpenfield).ToList();
        }

        // ---- Crop CRUD ----------------------------------------------------

        public async Task<IList<FarmOpenfieldCrop>> CropsGetAsync(int? tenantID)
        {
            IQueryable<FarmOpenfieldCropRow> q = db.FarmOpenfieldCrops.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(c => c.TenantID == tenantID);
            }
            var rows = await q.OrderBy(c => c.DisplayOrder).ThenBy(c => c.FarmOpenfieldCropName).ToListAsync();
            return rows.Select(ToDtoCrop).ToList();
        }

        public async Task<FarmOpenfieldCrop?> CropGetByIdAsync(int? idFarmOpenfieldCrop)
        {
            var row = await db.FarmOpenfieldCrops.AsNoTracking().FirstOrDefaultAsync(c => c.IDFarmOpenfieldCrop == idFarmOpenfieldCrop);
            return row == null ? null : ToDtoCrop(row);
        }

        public Task<FarmOpenfieldCrop> CropAddAsync(FarmOpenfieldCrop crop, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                int nextOrder = await db.FarmOpenfieldCrops.Where(c => c.TenantID == crop.TenantID).Select(c => (int?)c.DisplayOrder).MaxAsync() ?? -1;
                var row = new FarmOpenfieldCropRow { TenantID = crop.TenantID, FarmOpenfieldCropName = crop.FarmOpenfieldCropName, FarmOpenfieldID = crop.FarmOpenfieldID, DisplayOrder = nextOrder + 1 };
                db.FarmOpenfieldCrops.Add(row);
                await db.SaveChangesAsync();
                return ToDtoCrop(row);
            });

        public async Task CropUpdateAsync(FarmOpenfieldCrop crop)
        {
            var row = await db.FarmOpenfieldCrops.FirstOrDefaultAsync(c => c.IDFarmOpenfieldCrop == crop.IDFarmOpenfieldCrop);
            if (row == null)
            {
                return;
            }
            row.FarmOpenfieldCropName = crop.FarmOpenfieldCropName;
            await db.SaveChangesAsync();
        }

        public async Task CropsReorderAsync(int tenantId, IReadOnlyList<int> orderedCropIds)
        {
            var rows = await db.FarmOpenfieldCrops.Where(c => c.TenantID == tenantId && orderedCropIds.Contains(c.IDFarmOpenfieldCrop)).ToListAsync();
            var byId = rows.ToDictionary(c => c.IDFarmOpenfieldCrop);
            for (int i = 0; i < orderedCropIds.Count; i++)
            {
                if (byId.TryGetValue(orderedCropIds[i], out FarmOpenfieldCropRow? row))
                {
                    row.DisplayOrder = i;
                }
            }
            await db.SaveChangesAsync();
        }

        public async Task CropDeleteAsync(int idFarmOpenfieldCrop)
        {
            var parcelIds = await db.FarmOpenfieldCropParcels.AsNoTracking()
                .Where(p => p.FarmOpenfieldCropID == idFarmOpenfieldCrop)
                .Select(p => p.IDFarmOpenfieldCropParcel)
                .ToListAsync();

            foreach (int parcelId in parcelIds)
            {
                await ParcelDeleteAsync(parcelId);
            }

            // Crop-scope rules (DeviceFarmOpenfieldCropParcelID == null) live directly on the crop, not any of its parcels - the parcel loop above never touches them.
            var cropRuleIds = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmOpenfieldCropID == idFarmOpenfieldCrop && r.DeviceFarmOpenfieldCropParcelID == null).Select(r => r.IDDeviceFarmUnitZoneRule).ToListAsync();
            await db.RuleNotificationStates.Where(s => cropRuleIds.Contains(s.RuleID)).ExecuteDeleteAsync();
            await db.DeviceFarmUnitZoneRules.Where(r => r.DeviceFarmOpenfieldCropID == idFarmOpenfieldCrop && r.DeviceFarmOpenfieldCropParcelID == null).ExecuteDeleteAsync();

            await db.FarmOpenfieldCrops.Where(c => c.IDFarmOpenfieldCrop == idFarmOpenfieldCrop).ExecuteDeleteAsync();
        }

        // ---- Parcel CRUD ----------------------------------------------------

        public async Task<IList<FarmOpenfieldCropParcel>> ParcelsGetAsync(int idFarmOpenfieldCrop)
        {
            var rows = await db.FarmOpenfieldCropParcels.AsNoTracking()
                .Where(p => p.FarmOpenfieldCropID == idFarmOpenfieldCrop)
                .OrderBy(p => p.FarmOpenfieldCropParcelName)
                .ToListAsync();
            return rows.Select(ToDtoParcel).ToList();
        }

        public async Task<FarmOpenfieldCropParcel?> ParcelGetByIdAsync(int? idFarmOpenfieldCropParcel)
        {
            var row = await db.FarmOpenfieldCropParcels.AsNoTracking().FirstOrDefaultAsync(p => p.IDFarmOpenfieldCropParcel == idFarmOpenfieldCropParcel);
            return row == null ? null : ToDtoParcel(row);
        }

        public Task<FarmOpenfieldCropParcel> ParcelAddAsync(FarmOpenfieldCropParcel parcel, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                var row = new FarmOpenfieldCropParcelRow
                {
                    TenantID = parcel.TenantID,
                    FarmOpenfieldCropID = parcel.FarmOpenfieldCropID,
                    FarmOpenfieldCropParcelName = parcel.FarmOpenfieldCropParcelName,
                    WaterPumpMaxRunSeconds = settings.WaterPumpMaxRunSeconds,
                    WaterPumpCooldownSeconds = settings.WaterPumpCooldownSeconds,
                    TankCapacityLiters = parcel.TankCapacityLiters,
                    WaterLevelRawEmpty = parcel.WaterLevelRawEmpty,
                    WaterLevelRawFull = parcel.WaterLevelRawFull,
                    WaterPumpMinLevel = parcel.WaterPumpMinLevel,
                    HeatingMaxRunSeconds = parcel.HeatingMaxRunSeconds,
                    VentilationMaxRunSeconds = parcel.VentilationMaxRunSeconds,
                    HeatingFailSafePolicy = (int?)parcel.HeatingFailSafePolicy,
                };
                db.FarmOpenfieldCropParcels.Add(row);
                await db.SaveChangesAsync();
                return ToDtoParcel(row);
            });

        public async Task ParcelUpdateAsync(FarmOpenfieldCropParcel parcel)
        {
            var row = await db.FarmOpenfieldCropParcels.FirstOrDefaultAsync(p => p.IDFarmOpenfieldCropParcel == parcel.IDFarmOpenfieldCropParcel);
            if (row == null)
            {
                return;
            }
            row.FarmOpenfieldCropParcelName = parcel.FarmOpenfieldCropParcelName;
            row.WaterPumpMaxRunSeconds = parcel.WaterPumpMaxRunSeconds;
            row.WaterPumpCooldownSeconds = parcel.WaterPumpCooldownSeconds;
            row.SkipWaterPumpWhenRainPredicted = parcel.SkipWaterPumpWhenRainPredicted;
            row.TankCapacityLiters = parcel.TankCapacityLiters;
            row.WaterLevelRawEmpty = parcel.WaterLevelRawEmpty;
            row.WaterLevelRawFull = parcel.WaterLevelRawFull;
            row.WaterPumpMinLevel = parcel.WaterPumpMinLevel;
            row.HeatingMaxRunSeconds = parcel.HeatingMaxRunSeconds;
            row.VentilationMaxRunSeconds = parcel.VentilationMaxRunSeconds;
            row.HeatingFailSafePolicy = (int?)parcel.HeatingFailSafePolicy;
            await db.SaveChangesAsync();
            await ParcelConfigVersionBumpAsync(row.IDFarmOpenfieldCropParcel);
        }

        public async Task<bool> ParcelMigrateAsync(int idFarmOpenfieldCropParcel, int idTargetFarmOpenfieldCrop)
        {
            var row = await db.FarmOpenfieldCropParcels.FirstOrDefaultAsync(p => p.IDFarmOpenfieldCropParcel == idFarmOpenfieldCropParcel);
            if (row == null)
            {
                return false;
            }
            int? tenantID = row.TenantID;
            row.FarmOpenfieldCropID = idTargetFarmOpenfieldCrop;
            await db.SaveChangesAsync();
            await db.Devices.Where(d => d.FarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.FarmOpenfieldCropID, idTargetFarmOpenfieldCrop));
            await ParcelConfigVersionBumpAsync(idFarmOpenfieldCropParcel);
            await deviceRepository.InvalidateFleetCacheAsync(tenantID);
            return true;
        }

        public async Task ParcelConfigVersionBumpAsync(int idFarmOpenfieldCropParcel)
        {
            await db.Devices.Where(d => d.FarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ConfigVersion, d => (d.ConfigVersion ?? 0) + 1));
        }

        public async Task ParcelDeleteAsync(int idFarmOpenfieldCropParcel)
        {
            var deviceIds = await db.Devices.AsNoTracking()
                .Where(d => d.FarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel)
                .Select(d => d.IDDevice)
                .ToListAsync();

            foreach (int deviceId in deviceIds)
            {
                await DeviceUnassignFromParcelAsync(deviceId);
            }

            var ruleIds = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel).Select(r => r.IDDeviceFarmUnitZoneRule).ToListAsync();
            await db.RuleNotificationStates.Where(s => ruleIds.Contains(s.RuleID)).ExecuteDeleteAsync();
            await db.DeviceFarmUnitZoneRules.Where(r => r.DeviceFarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel).ExecuteDeleteAsync();

            await db.FarmOpenfieldCropParcels.Where(p => p.IDFarmOpenfieldCropParcel == idFarmOpenfieldCropParcel).ExecuteDeleteAsync();
        }

        // ---- Device assignment -----------------------------------------

        public async Task DeviceAssignToParcelAsync(int idDevice, int idFarmOpenfieldCropParcel)
        {
            var parcel = await db.FarmOpenfieldCropParcels.AsNoTracking().FirstOrDefaultAsync(p => p.IDFarmOpenfieldCropParcel == idFarmOpenfieldCropParcel);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == idDevice);
            if (parcel == null || device == null)
            {
                return;
            }

            device.FarmOpenfieldCropID = parcel.FarmOpenfieldCropID;
            device.FarmOpenfieldCropParcelID = parcel.IDFarmOpenfieldCropParcel;
            // Mutual exclusivity - a device moving onto the Open-Field branch can't still be on the Greenhouse one (see EfDeviceFarmUnitRepository.DeviceUnassignedGetAsync's own filter).
            device.DeviceFarmUnitID = null;
            device.DeviceFarmUnitZoneID = null;
            device.ConfigVersion = (device.ConfigVersion ?? 0) + 1;
            await db.SaveChangesAsync();
            await deviceRepository.InvalidateFleetCacheAsync(device.TenantID);
        }

        public async Task<bool> ParcelHasControllerAsync(int idFarmOpenfieldCropParcel) =>
            await db.Devices.AsNoTracking().AnyAsync(d => d.FarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel && d.DeviceControllerEnabled == true);

        public async Task DeviceUnassignFromParcelAsync(int idDevice)
        {
            int? tenantID = await db.Devices.AsNoTracking()
                .Where(d => d.IDDevice == idDevice).Select(d => (int?)d.TenantID).FirstOrDefaultAsync();
            await db.Devices.Where(d => d.IDDevice == idDevice)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.FarmOpenfieldCropID, (int?)null)
                    .SetProperty(d => d.FarmOpenfieldCropParcelID, (int?)null));
            await deviceRepository.InvalidateFleetCacheAsync(tenantID);
        }

        // ---- Dashboard widget aggregation -----------------

        public async Task<(SensorAverages Averages, SensorTrend Trend)> CropAggregateAsync(int idFarmOpenfieldCrop)
        {
            IQueryable<DeviceRow> scopedDevices = db.Devices.AsNoTracking().Where(d => d.FarmOpenfieldCropID == idFarmOpenfieldCrop);
            (int expiryHours, bool alertsEnabled) = await ProblemEventSettingsAsync();
            var snapshots = await GetDeviceSnapshotsAsync(scopedDevices, expiryHours, alertsEnabled);
            var parcelIds = await db.FarmOpenfieldCropParcels.AsNoTracking()
                .Where(p => p.FarmOpenfieldCropID == idFarmOpenfieldCrop)
                .Select(p => p.IDFarmOpenfieldCropParcel)
                .ToListAsync();
            return (Average(snapshots), await BuildTrendAsync(parcelIds));
        }

        public async Task<(SensorAverages Averages, SensorTrend Trend)> ParcelAggregateAsync(int idFarmOpenfieldCropParcel)
        {
            IQueryable<DeviceRow> scopedDevices = db.Devices.AsNoTracking().Where(d => d.FarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel);
            (int expiryHours, bool alertsEnabled) = await ProblemEventSettingsAsync();
            var snapshots = await GetDeviceSnapshotsAsync(scopedDevices, expiryHours, alertsEnabled);
            return (Average(snapshots), await BuildTrendAsync([idFarmOpenfieldCropParcel]));
        }

        private async Task<(int ExpiryHours, bool AlertsEnabled)> ProblemEventSettingsAsync()
        {
            ServerConfig config = await serverConfigRepository.ServerConfigGetAsync(1);
            int expiryHours = config.ProblemEventExpiryHours > 0 ? config.ProblemEventExpiryHours : 24;
            return (expiryHours, config.ProblemEventAlertsEnabled);
        }

        /// Same "latest telemetry per device via portable scalar subqueries" shape as EfDeviceFarmUnitRepository.GetDeviceSnapshotsAsync.
        private async Task<List<CropParcelDeviceSnapshot>> GetDeviceSnapshotsAsync(IQueryable<DeviceRow> devices, int problemEventExpiryHours, bool problemEventAlertsEnabled)
        {
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            DateTimeOffset problemEventCutoff = utcNow.AddHours(-problemEventExpiryHours);

            var deviceLatestIds = await devices
                .Select(d => new
                {
                    d.Enabled,
                    d.SleepSeconds,
                    LastSeenAt = db.DeviceDiagnostics.AsNoTracking()
                        .Where(x => x.DeviceID == d.IDDevice)
                        .Select(x => x.LastSeenAt)
                        .FirstOrDefault(),
                    HasRecentProblemEvent = problemEventAlertsEnabled && db.EventDevices.AsNoTracking()
                        .Any(e => e.DeviceID == d.IDDevice && e.AcknowledgedAt == null && e.Date >= problemEventCutoff && ProblemEventTypeIds.Contains(e.EventID)),
                    LatestSensorDataId = db.SensorData.AsNoTracking()
                        .Where(s => s.DeviceID == d.IDDevice)
                        .OrderByDescending(s => s.DateCreated)
                        .Select(s => (int?)s.IDSensorData)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            var latestIds = deviceLatestIds.Where(x => x.LatestSensorDataId != null).Select(x => x.LatestSensorDataId!.Value).ToList();
            var latestById = latestIds.Count == 0
                ? new Dictionary<int, SensorDataRow>()
                : await db.SensorData.AsNoTracking()
                    .Where(s => latestIds.Contains(s.IDSensorData))
                    .ToDictionaryAsync(s => s.IDSensorData);

            return deviceLatestIds.Select(d =>
            {
                SensorDataRow? s = d.LatestSensorDataId != null && latestById.TryGetValue(d.LatestSensorDataId.Value, out var row) ? row : null;
                double maxReadingAgeSeconds = (d.SleepSeconds ?? 60) * DeviceFleetStatus.OfflineMissedPolls + DeviceFleetStatus.OfflineGraceSeconds;
                if (s?.DateCreated is DateTimeOffset readingAt && (utcNow - readingAt).TotalSeconds > maxReadingAgeSeconds)
                {
                    s = null;
                }
                bool enabled = d.Enabled == true;
                bool online = !enabled || DeviceFleetStatus.ComputeOnline(d.LastSeenAt, d.SleepSeconds, utcNow);
                return new CropParcelDeviceSnapshot(
                    enabled, online, d.HasRecentProblemEvent,
                    s?.Temperature, s?.SoilTemperature, s?.Humidity, s?.Moisture, s?.Light,
                    s?.Co2, s?.Tvoc, s?.Barometer, s?.LiquidPH, s?.RainLevel, s?.WaterLevel, s?.Wind,
                    s?.Ec, s?.Weight);
            }).ToList();
        }

        private static SensorAverages Average(IReadOnlyCollection<CropParcelDeviceSnapshot> snapshots)
        {
            return new SensorAverages
            {
                Temperature = snapshots.Select(s => s.Temperature).Average(),
                SoilTemperature = snapshots.Select(s => s.SoilTemperature).Average(),
                Humidity = snapshots.Select(s => s.Humidity).Average(),
                Vpd = snapshots.Select(s => s.Vpd).Average(),
                DewPoint = snapshots.Select(s => s.DewPoint).Average(),
                DewPointSpread = snapshots.Select(s => s.DewPointSpread).Average(),
                Moisture = snapshots.Select(s => s.Moisture).Average(),
                Light = snapshots.Select(s => s.Light).Average(),
                Co2 = snapshots.Select(s => s.Co2).Average(),
                Tvoc = snapshots.Select(s => s.Tvoc).Average(),
                Barometer = snapshots.Select(s => s.Barometer).Average(),
                LiquidPH = snapshots.Select(s => s.LiquidPH).Average(),
                RainLevel = snapshots.Select(s => s.RainLevel).Average(),
                WaterLevel = snapshots.Select(s => s.WaterLevel).Average(),
                Wind = snapshots.Select(s => s.Wind).Average(),
                Ec = snapshots.Select(s => s.Ec).Average(),
                Weight = snapshots.Select(s => s.Weight).Average(),
            };
        }

        /// Same "24h hourly bucket" shape as EfDeviceFarmUnitRepository.BuildTrendAsync, filtered by FarmOpenfieldCropParcelID instead of DeviceFarmUnitZoneID.
        private async Task<SensorTrend> BuildTrendAsync(List<int> parcelIds)
        {
            var trend = new SensorTrend();
            if (parcelIds.Count == 0)
            {
                return trend;
            }

            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            DateTimeOffset cutoff = utcNow.AddHours(-SensorTrend.HourBuckets);

            var rows = await db.SensorData.AsNoTracking()
                .Where(s => s.FarmOpenfieldCropParcelID != null && parcelIds.Contains(s.FarmOpenfieldCropParcelID.Value) && s.DateCreated >= cutoff)
                .Select(s => new
                {
                    s.DateCreated, s.Temperature, s.SoilTemperature, s.Humidity, s.Moisture, s.Light,
                    s.Co2, s.Tvoc, s.Barometer, s.LiquidPH, s.RainLevel, s.WaterLevel, s.Wind, s.Ec, s.Weight,
                })
                .ToListAsync();

            var byBucket = rows
                .Where(r => r.DateCreated != null)
                .Select(r => (Bucket: HourBucketIndex(r.DateCreated!.Value, utcNow), Row: r))
                .Where(x => x.Bucket >= 0 && x.Bucket < SensorTrend.HourBuckets)
                .GroupBy(x => x.Bucket, x => x.Row);

            foreach (var bucket in byBucket)
            {
                var rowsInBucket = bucket.ToList();
                trend.Temperature[bucket.Key] = rowsInBucket.Select(r => r.Temperature).Average();
                trend.SoilTemperature[bucket.Key] = rowsInBucket.Select(r => r.SoilTemperature).Average();
                trend.Humidity[bucket.Key] = rowsInBucket.Select(r => r.Humidity).Average();
                trend.Vpd[bucket.Key] = rowsInBucket.Select(r => VpdCalculator.Compute(r.Temperature, r.Humidity)).Average();
                trend.DewPoint[bucket.Key] = rowsInBucket.Select(r => DewPointCalculator.Compute(r.Temperature, r.Humidity)).Average();
                trend.DewPointSpread[bucket.Key] = rowsInBucket.Select(r => r.Temperature - DewPointCalculator.Compute(r.Temperature, r.Humidity)).Average();
                trend.Moisture[bucket.Key] = rowsInBucket.Select(r => r.Moisture).Average();
                trend.Light[bucket.Key] = rowsInBucket.Select(r => r.Light).Average();
                trend.Co2[bucket.Key] = rowsInBucket.Select(r => r.Co2).Average();
                trend.Tvoc[bucket.Key] = rowsInBucket.Select(r => r.Tvoc).Average();
                trend.Barometer[bucket.Key] = rowsInBucket.Select(r => r.Barometer).Average();
                trend.LiquidPH[bucket.Key] = rowsInBucket.Select(r => r.LiquidPH).Average();
                trend.RainLevel[bucket.Key] = rowsInBucket.Select(r => r.RainLevel).Average();
                trend.WaterLevel[bucket.Key] = rowsInBucket.Select(r => r.WaterLevel).Average();
                trend.Wind[bucket.Key] = rowsInBucket.Select(r => r.Wind).Average();
                trend.Ec[bucket.Key] = rowsInBucket.Select(r => r.Ec).Average();
                trend.Weight[bucket.Key] = rowsInBucket.Select(r => r.Weight).Average();
            }
            return trend;
        }

        private static int HourBucketIndex(DateTimeOffset readingAt, DateTimeOffset utcNow) =>
            SensorTrend.HourBuckets - 1 - (int)Math.Floor((utcNow - readingAt).TotalHours);

        private static FarmOpenfield ToDtoOpenfield(FarmOpenfieldRow o) => new()
        {
            IDFarmOpenfield = o.IDFarmOpenfield,
            TenantID = o.TenantID,
            FarmID = o.FarmID,
            DeletedAtUtc = o.DeletedAtUtc,
        };

        private static FarmOpenfieldCrop ToDtoCrop(FarmOpenfieldCropRow c) => new()
        {
            IDFarmOpenfieldCrop = c.IDFarmOpenfieldCrop,
            TenantID = c.TenantID,
            FarmOpenfieldCropName = c.FarmOpenfieldCropName,
            FarmOpenfieldID = c.FarmOpenfieldID,
            DisplayOrder = c.DisplayOrder,
        };

        private static FarmOpenfieldCropParcel ToDtoParcel(FarmOpenfieldCropParcelRow p) => new()
        {
            IDFarmOpenfieldCropParcel = p.IDFarmOpenfieldCropParcel,
            TenantID = p.TenantID,
            FarmOpenfieldCropID = p.FarmOpenfieldCropID,
            FarmOpenfieldCropParcelName = p.FarmOpenfieldCropParcelName,
            WaterPumpMaxRunSeconds = p.WaterPumpMaxRunSeconds,
            WaterPumpCooldownSeconds = p.WaterPumpCooldownSeconds,
            SkipWaterPumpWhenRainPredicted = p.SkipWaterPumpWhenRainPredicted,
            TankCapacityLiters = p.TankCapacityLiters,
            WaterLevelRawEmpty = p.WaterLevelRawEmpty,
            WaterLevelRawFull = p.WaterLevelRawFull,
            WaterPumpMinLevel = p.WaterPumpMinLevel,
            HeatingMaxRunSeconds = p.HeatingMaxRunSeconds,
            VentilationMaxRunSeconds = p.VentilationMaxRunSeconds,
            HeatingFailSafePolicy = (HeatingFailSafePolicyType?)p.HeatingFailSafePolicy,
            DashboardWidgets = string.IsNullOrEmpty(p.DashboardWidgetsJson)
                ? []
                : System.Text.Json.JsonSerializer.Deserialize<List<DashboardWidget>>(p.DashboardWidgetsJson) ?? [],
        };
    }
}
