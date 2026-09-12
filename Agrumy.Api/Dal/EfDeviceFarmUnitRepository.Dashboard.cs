using Agrumy.Dal;
using Agrumy.Shared;
using System.Text.Json;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Dal
{
    internal sealed partial class EfDeviceFarmUnitRepository
    {
        // ---- Dashboard aggregation -------------------------------------

        public async Task<IList<DeviceFarmUnitDashboard>> DeviceFarmUnitDashboardGetAsync(int? tenantID)
        {
            IQueryable<DeviceFarmUnitRow> units = db.DeviceFarmUnits.AsNoTracking();
            if (tenantID != null)
            {
                units = units.Where(u => u.TenantID == tenantID);
            }
            var unitRows = await units.OrderBy(u => u.DisplayOrder).ThenBy(u => u.DeviceFarmUnitName).ToListAsync();

            IQueryable<DeviceRow> scopedDevices = db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitID != null);
            if (tenantID != null)
            {
                scopedDevices = scopedDevices.Where(d => d.TenantID == tenantID);
            }
            (int expiryHours, bool alertsEnabled) = await ProblemEventSettingsAsync(tenantID);
            var snapshots = await GetDeviceSnapshotsAsync(scopedDevices, expiryHours, alertsEnabled);
            var alerts = await GetProblemAlertsAsync(scopedDevices, expiryHours, alertsEnabled);

            var zonesByUnit = (await db.DeviceFarmUnitZones.AsNoTracking()
                .Select(z => new { z.DeviceFarmUnitID, z.IDDeviceFarmUnitZone })
                .ToListAsync())
                .GroupBy(z => z.DeviceFarmUnitID)
                .ToDictionary(g => g.Key, g => g.Select(z => z.IDDeviceFarmUnitZone).ToList());

            // One SensorData query for every unit's zones combined, not one query per unit in the loop below - a fleet with 20+ units otherwise pulls its whole 24h trend window once per unit.
            var zoneIdsByUnit = unitRows.ToDictionary(u => u.IDDeviceFarmUnit, u => zonesByUnit.GetValueOrDefault(u.IDDeviceFarmUnit) ?? []);
            var trendsByUnit = await BuildTrendsByZoneGroupAsync(zoneIdsByUnit);

            var result = new List<DeviceFarmUnitDashboard>();
            foreach (var u in unitRows)
            {
                var scoped = snapshots.Where(s => s.DeviceFarmUnitID == u.IDDeviceFarmUnit).ToList();
                var zoneIds = zonesByUnit.GetValueOrDefault(u.IDDeviceFarmUnit) ?? [];
                result.Add(new DeviceFarmUnitDashboard
                {
                    IDDeviceFarmUnit = u.IDDeviceFarmUnit,
                    DeviceFarmUnitName = u.DeviceFarmUnitName,
                    DeviceFarmID = u.DeviceFarmID,
                    DisplayOrder = u.DisplayOrder,
                    ZoneCount = zoneIds.Count,
                    DeviceCount = scoped.Count,
                    Averages = Average(scoped),
                    Status = ComputeStatus(scoped),
                    Trend = trendsByUnit[u.IDDeviceFarmUnit],
                    ProblemAlerts = alerts.Where(a => a.DeviceFarmUnitID == u.IDDeviceFarmUnit).Select(ToDtoAlert).ToList(),
                });
            }
            return result;
        }

        public async Task<IList<DeviceFarmUnitZoneDashboard>> DeviceFarmUnitZoneDashboardListGetAsync(int idDeviceFarmUnit)
        {
            var zoneRows = await db.DeviceFarmUnitZones.AsNoTracking()
                .Where(z => z.DeviceFarmUnitID == idDeviceFarmUnit)
                .ToListAsync();

            IQueryable<DeviceRow> scopedDevices = db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitID == idDeviceFarmUnit);
            (int expiryHours, bool alertsEnabled) = await ProblemEventSettingsAsync(zoneRows.Count > 0 ? zoneRows[0].TenantID : null);
            var snapshots = await GetDeviceSnapshotsAsync(scopedDevices, expiryHours, alertsEnabled);
            var alerts = await GetProblemAlertsAsync(scopedDevices, expiryHours, alertsEnabled);

            // Same batching as DeviceFarmUnitDashboardGetAsync - one query for every zone's trend instead of one per zone in the loop below.
            var trendsByZone = await BuildTrendsByZoneGroupAsync(zoneRows.ToDictionary(z => z.IDDeviceFarmUnitZone, z => (List<int>)[z.IDDeviceFarmUnitZone]));

            var result = new List<DeviceFarmUnitZoneDashboard>();
            foreach (var z in zoneRows)
            {
                var scoped = snapshots.Where(s => s.DeviceFarmUnitZoneID == z.IDDeviceFarmUnitZone).ToList();
                result.Add(new DeviceFarmUnitZoneDashboard
                {
                    IDDeviceFarmUnitZone = z.IDDeviceFarmUnitZone,
                    IDDeviceFarmUnit = z.DeviceFarmUnitID,
                    DeviceFarmUnitZoneName = z.DeviceFarmUnitZoneName,
                    DeviceCount = scoped.Count,
                    Averages = Average(scoped, z),
                    Status = ComputeStatus(scoped),
                    Trend = trendsByZone[z.IDDeviceFarmUnitZone],
                    ProblemAlerts = alerts.Where(a => a.DeviceFarmUnitZoneID == z.IDDeviceFarmUnitZone).Select(ToDtoAlert).ToList(),
                });
            }
            return result;
        }

        public Task<DeviceFarmUnitZoneDashboard?> DeviceFarmUnitZoneDashboardGetAsync(int idDeviceFarmUnitZone) =>
            BuildZoneDashboardAsync(idDeviceFarmUnitZone);

        public async Task<DeviceFarmUnitZoneDashboard?> DeviceFarmUnitZoneDashboardForDisplayGetAsync(int idDeviceFarmUnitZone)
        {
            // Dirty reads are fine for a display snapshot - same reasoning as SensorDataExportGetAsync; Postgres treats this as ReadCommitted regardless (MVCC readers never block writers there).
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);
            return await BuildZoneDashboardAsync(idDeviceFarmUnitZone);
        }

        /// Shared by both isolation-level variants above - the query shape itself never differs, only which transaction (if any) wraps it.
        private async Task<DeviceFarmUnitZoneDashboard?> BuildZoneDashboardAsync(int idDeviceFarmUnitZone)
        {
            var zone = await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            if (zone == null)
            {
                return null;
            }

            var deviceRows = await db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone).ToListAsync();
            IQueryable<DeviceRow> scopedDevices = db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone);
            (int expiryHours, bool alertsEnabled) = await ProblemEventSettingsAsync(zone.TenantID);
            var snapshots = await GetDeviceSnapshotsAsync(scopedDevices, expiryHours, alertsEnabled);
            var alerts = await GetProblemAlertsAsync(scopedDevices, expiryHours, alertsEnabled);

            return new DeviceFarmUnitZoneDashboard
            {
                IDDeviceFarmUnitZone = zone.IDDeviceFarmUnitZone,
                IDDeviceFarmUnit = zone.DeviceFarmUnitID,
                DeviceFarmUnitZoneName = zone.DeviceFarmUnitZoneName,
                DeviceCount = deviceRows.Count,
                Averages = Average(snapshots, zone),
                Devices = deviceRows.Select(EfDeviceRepository.ToDto).ToList(),
                Status = ComputeStatus(snapshots),
                Trend = await BuildTrendAsync([idDeviceFarmUnitZone]),
                ProblemAlerts = alerts.Select(ToDtoAlert).ToList(),
            };
        }

        // ---- Dashboard widget aggregation -----------------

        /// One widget's own (level, levelId) scope, independent of whichever zone's page displays it.
        public async Task<DashboardAggregate> DashboardAggregateGetAsync(HierarchyNodeKind level, int levelId)
        {
            (SensorAverages averages, SensorTrend trend) = level switch
            {
                HierarchyNodeKind.Farm => await BuildFarmAggregateAsync(levelId),
                HierarchyNodeKind.Unit => await BuildUnitAggregateAsync(levelId),
                HierarchyNodeKind.Zone => await BuildZoneAggregateAsync(levelId),
                HierarchyNodeKind.Sowing => await sowingRepository.SowingAggregateAsync(levelId),
                HierarchyNodeKind.FarmParcelZone => await farmParcelRepository.FarmParcelZoneAggregateAsync(levelId),
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown dashboard aggregation level"),
            };
            return new DashboardAggregate { Averages = averages, Trend = trend };
        }

        private async Task<(SensorAverages Averages, SensorTrend Trend)> BuildZoneAggregateAsync(int idDeviceFarmUnitZone)
        {
            DeviceFarmUnitZoneDashboard? dashboard = await BuildZoneDashboardAsync(idDeviceFarmUnitZone);
            return dashboard == null ? (new SensorAverages(), new SensorTrend()) : (dashboard.Averages, dashboard.Trend);
        }

        private async Task<(SensorAverages Averages, SensorTrend Trend)> BuildUnitAggregateAsync(int idDeviceFarmUnit)
        {
            IQueryable<DeviceRow> scopedDevices = db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitID == idDeviceFarmUnit);
            // Averages-only caller (see BuildZoneAggregateAsync's sibling for the Status-carrying path) - HasRecentProblemEvent goes unused here, so the server-wide default is fine regardless of tenant.
            (int expiryHours, bool alertsEnabled) = await ProblemEventSettingsAsync(null);
            var snapshots = await GetDeviceSnapshotsAsync(scopedDevices, expiryHours, alertsEnabled);
            var zoneIds = await db.DeviceFarmUnitZones.AsNoTracking()
                .Where(z => z.DeviceFarmUnitID == idDeviceFarmUnit)
                .Select(z => z.IDDeviceFarmUnitZone)
                .ToListAsync();
            return (Average(snapshots), await BuildTrendAsync(zoneIds));
        }

        /// Rolls up every zone under every unit of the given farm - the one aggregation level that didn't already have an existing helper elsewhere in this file to reuse.
        private async Task<(SensorAverages Averages, SensorTrend Trend)> BuildFarmAggregateAsync(int idDeviceFarm)
        {
            var unitIds = await db.DeviceFarmUnits.AsNoTracking()
                .Where(u => u.DeviceFarmID == idDeviceFarm)
                .Select(u => u.IDDeviceFarmUnit)
                .ToListAsync();
            IQueryable<DeviceRow> scopedDevices = db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitID != null && unitIds.Contains(d.DeviceFarmUnitID.Value));
            // Averages-only caller, same reasoning as BuildUnitAggregateAsync above.
            (int expiryHours, bool alertsEnabled) = await ProblemEventSettingsAsync(null);
            var snapshots = await GetDeviceSnapshotsAsync(scopedDevices, expiryHours, alertsEnabled);
            var zoneIds = await db.DeviceFarmUnitZones.AsNoTracking()
                .Where(z => unitIds.Contains(z.DeviceFarmUnitID))
                .Select(z => z.IDDeviceFarmUnitZone)
                .ToListAsync();
            return (Average(snapshots), await BuildTrendAsync(zoneIds));
        }

        /// Shared by every dashboard aggregation call this request needs it in - tenantID's own TenantAlertConfig override wins over the ServerConfig default; null (a cross-tenant combined view, or a call site where Status/ProblemAlerts is never read) falls back to the server-wide default.
        private async Task<(int ExpiryHours, bool AlertsEnabled)> ProblemEventSettingsAsync(int? tenantID)
        {
            ServerConfig config = await serverConfigRepository.ServerConfigGetAsync(1);
            bool? tenantAlertsEnabled = null;
            int? tenantExpiryHours = null;
            if (tenantID is int id)
            {
                var row = await db.Tenants.AsNoTracking().Where(t => t.IDTenant == id)
                    .Select(t => new { t.ProblemEventAlertsEnabled, t.ProblemEventExpiryHours }).FirstOrDefaultAsync();
                tenantAlertsEnabled = row?.ProblemEventAlertsEnabled;
                tenantExpiryHours = row?.ProblemEventExpiryHours;
            }
            int expiryHoursRaw = tenantExpiryHours ?? config.ProblemEventExpiryHours;
            int expiryHours = expiryHoursRaw > 0 ? expiryHoursRaw : 24;
            return (expiryHours, tenantAlertsEnabled ?? config.ProblemEventAlertsEnabled);
        }

        /// Latest telemetry per device - EF can't translate a whole-row correlated subquery, so this pulls the latest SensorData id per device via portable scalar subqueries, then batch-fetches the rows.
        private async Task<List<UnitZoneDeviceSnapshot>> GetDeviceSnapshotsAsync(IQueryable<DeviceRow> devices, int problemEventExpiryHours, bool problemEventAlertsEnabled)
        {
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            DateTimeOffset problemEventCutoff = utcNow.AddHours(-problemEventExpiryHours);

            var deviceLatestIds = await devices
                .Select(d => new
                {
                    d.DeviceFarmUnitID,
                    d.DeviceFarmUnitZoneID,
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
                // Same window ComputeOnline uses to decide online/offline - a reading outside it is from a dead/unreachable sensor and must not silently count toward the zone/unit average or feed RuleNotificationEvaluator (which reads this same Averages value).
                double maxReadingAgeSeconds = (d.SleepSeconds ?? 60) * DeviceFleetStatus.OfflineMissedPolls + DeviceFleetStatus.OfflineGraceSeconds;
                if (s?.DateCreated is DateTimeOffset readingAt && (utcNow - readingAt).TotalSeconds > maxReadingAgeSeconds)
                {
                    s = null;
                }
                bool enabled = d.Enabled == true;
                // A disabled device is expected to be silent - its offline-ness must not redden a zone/unit nobody expects it to report into.
                bool online = !enabled || DeviceFleetStatus.ComputeOnline(d.LastSeenAt, d.SleepSeconds, utcNow);
                return new UnitZoneDeviceSnapshot(
                    d.DeviceFarmUnitID, d.DeviceFarmUnitZoneID, enabled, online, d.HasRecentProblemEvent,
                    s?.Temperature, s?.SoilTemperature, s?.Humidity, s?.Moisture, s?.Light,
                    s?.Co2, s?.Tvoc, s?.Barometer, s?.LiquidPH, s?.RainLevel, s?.WaterLevel, s?.Wind,
                    s?.Ec, s?.Weight);
            }).ToList();
        }

        /// Carries JOIN projection fields - lets the alert list group by unit/zone without a second round trip to look either up from DeviceID.
        private sealed record UnitZoneProblemAlertRow(int? DeviceFarmUnitID, int? DeviceFarmUnitZoneID, int IDEventDevice, int DeviceID, string? DeviceName, int EventID, DateTimeOffset? Date, string? Message);

        /// Every un-acknowledged problem event still inside the expiry window - same predicate as GetDeviceSnapshotsAsync's HasRecentProblemEvent, but returns the actual rows so the dashboard can show what triggered Orange.
        private async Task<List<UnitZoneProblemAlertRow>> GetProblemAlertsAsync(IQueryable<DeviceRow> devices, int problemEventExpiryHours, bool problemEventAlertsEnabled)
        {
            if (!problemEventAlertsEnabled)
            {
                return [];
            }

            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-problemEventExpiryHours);
            // OrderByDescending must run before the final Select - EF cannot translate ordering by a member of a record it just constructed inside the join's own result selector.
            var rows = await devices
                .Join(
                    db.EventDevices.AsNoTracking().Where(e => e.AcknowledgedAt == null && e.Date >= cutoff && ProblemEventTypeIds.Contains(e.EventID)),
                    d => d.IDDevice, e => e.DeviceID,
                    (d, e) => new { d.DeviceFarmUnitID, d.DeviceFarmUnitZoneID, e.IDEventDevice, d.IDDevice, d.DeviceName, e.EventID, e.Date, e.Message })
                .OrderByDescending(a => a.Date)
                .ToListAsync();
            return rows.Select(a => new UnitZoneProblemAlertRow(a.DeviceFarmUnitID, a.DeviceFarmUnitZoneID, a.IDEventDevice, a.IDDevice, a.DeviceName, a.EventID, a.Date, a.Message)).ToList();
        }

        private static UnitZoneProblemAlert ToDtoAlert(UnitZoneProblemAlertRow a) => new()
        {
            IDEventDevice = a.IDEventDevice,
            DeviceID = a.DeviceID,
            DeviceName = a.DeviceName,
            EventType = Enum.IsDefined(typeof(DeviceEventType), a.EventID) ? ((DeviceEventType)a.EventID).ToString() : $"Unknown({a.EventID})",
            Date = a.Date,
            Message = a.Message,
        };

        /// Red beats Orange beats Green - only enabled devices' online state counts toward Red, so a disabled-but-offline device turns the zone/unit Orange instead of Green.
        private static ZoneStatus ComputeStatus(IReadOnlyCollection<UnitZoneDeviceSnapshot> snapshots)
        {
            if (snapshots.Any(s => s.Enabled && !s.Online))
            {
                return ZoneStatus.Red;
            }
            if (snapshots.Any(s => s.HasRecentProblemEvent) || snapshots.Any(s => !s.Enabled))
            {
                return ZoneStatus.Orange;
            }
            return ZoneStatus.Green;
        }

        /// Last-24h hourly average per sensor type across the given zones - filters sensorData directly by DeviceFarmUnitZoneID (ix_sensorData_deviceFarmUnitZone_date) since a trend needs every reading, not just the latest.
        private async Task<SensorTrend> BuildTrendAsync(List<int> zoneIds)
        {
            var trend = new SensorTrend();
            if (zoneIds.Count == 0)
            {
                return trend;
            }

            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            DateTimeOffset cutoff = utcNow.AddHours(-SensorTrend.HourBuckets);

            var rows = await db.SensorData.AsNoTracking()
                .Where(s => s.DeviceFarmUnitZoneID != null && zoneIds.Contains(s.DeviceFarmUnitZoneID.Value) && s.DateCreated >= cutoff)
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

        /// Same 24h hourly-bucket trend as BuildTrendAsync, but for many keys (units, or zones) in one SensorData query instead of one query per key - each zone belongs to exactly one key, so results just get routed by DeviceFarmUnitZoneID after the single fetch.
        private async Task<Dictionary<TKey, SensorTrend>> BuildTrendsByZoneGroupAsync<TKey>(Dictionary<TKey, List<int>> zoneIdsByKey) where TKey : notnull
        {
            var result = zoneIdsByKey.Keys.ToDictionary(k => k, _ => new SensorTrend());
            var keyByZoneId = new Dictionary<int, TKey>();
            foreach (var (key, zoneIds) in zoneIdsByKey)
            {
                foreach (int zoneId in zoneIds)
                {
                    keyByZoneId[zoneId] = key;
                }
            }
            if (keyByZoneId.Count == 0)
            {
                return result;
            }

            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            DateTimeOffset cutoff = utcNow.AddHours(-SensorTrend.HourBuckets);
            var zoneIdList = keyByZoneId.Keys.ToList();

            var rows = await db.SensorData.AsNoTracking()
                .Where(s => s.DeviceFarmUnitZoneID != null && zoneIdList.Contains(s.DeviceFarmUnitZoneID.Value) && s.DateCreated >= cutoff)
                .Select(s => new
                {
                    s.DeviceFarmUnitZoneID, s.DateCreated, s.Temperature, s.SoilTemperature, s.Humidity, s.Moisture, s.Light,
                    s.Co2, s.Tvoc, s.Barometer, s.LiquidPH, s.RainLevel, s.WaterLevel, s.Wind, s.Ec, s.Weight,
                })
                .ToListAsync();

            var byKeyAndBucket = rows
                .Where(r => r.DateCreated != null)
                .Select(r => (Key: keyByZoneId[r.DeviceFarmUnitZoneID!.Value], Bucket: HourBucketIndex(r.DateCreated!.Value, utcNow), Row: r))
                .Where(x => x.Bucket >= 0 && x.Bucket < SensorTrend.HourBuckets)
                .GroupBy(x => (x.Key, x.Bucket));

            foreach (var group in byKeyAndBucket)
            {
                var trend = result[group.Key.Key];
                var rowsInBucket = group.Select(x => x.Row).ToList();
                trend.Temperature[group.Key.Bucket] = rowsInBucket.Select(r => r.Temperature).Average();
                trend.SoilTemperature[group.Key.Bucket] = rowsInBucket.Select(r => r.SoilTemperature).Average();
                trend.Humidity[group.Key.Bucket] = rowsInBucket.Select(r => r.Humidity).Average();
                trend.Vpd[group.Key.Bucket] = rowsInBucket.Select(r => VpdCalculator.Compute(r.Temperature, r.Humidity)).Average();
                trend.DewPoint[group.Key.Bucket] = rowsInBucket.Select(r => DewPointCalculator.Compute(r.Temperature, r.Humidity)).Average();
                trend.DewPointSpread[group.Key.Bucket] = rowsInBucket.Select(r => r.Temperature - DewPointCalculator.Compute(r.Temperature, r.Humidity)).Average();
                trend.Moisture[group.Key.Bucket] = rowsInBucket.Select(r => r.Moisture).Average();
                trend.Light[group.Key.Bucket] = rowsInBucket.Select(r => r.Light).Average();
                trend.Co2[group.Key.Bucket] = rowsInBucket.Select(r => r.Co2).Average();
                trend.Tvoc[group.Key.Bucket] = rowsInBucket.Select(r => r.Tvoc).Average();
                trend.Barometer[group.Key.Bucket] = rowsInBucket.Select(r => r.Barometer).Average();
                trend.LiquidPH[group.Key.Bucket] = rowsInBucket.Select(r => r.LiquidPH).Average();
                trend.RainLevel[group.Key.Bucket] = rowsInBucket.Select(r => r.RainLevel).Average();
                trend.WaterLevel[group.Key.Bucket] = rowsInBucket.Select(r => r.WaterLevel).Average();
                trend.Wind[group.Key.Bucket] = rowsInBucket.Select(r => r.Wind).Average();
                trend.Ec[group.Key.Bucket] = rowsInBucket.Select(r => r.Ec).Average();
                trend.Weight[group.Key.Bucket] = rowsInBucket.Select(r => r.Weight).Average();
            }
            return result;
        }

        /// 0 = the bucket ending 24h ago, 23 = the current hour - a timestamp outside the 24h window (or, defensively, in the future) falls outside [0, HourBuckets), which the caller filters out.
        private static int HourBucketIndex(DateTimeOffset dateCreated, DateTimeOffset utcNow) =>
            SensorTrend.HourBuckets - 1 - (int)Math.Floor((utcNow - dateCreated).TotalHours);

        /// Per-sensor-type average across snapshots - LINQ's nullable Average() already ignores nulls and returns null (not an exception) for an all-null source, exactly "no device reported this type". zone is only passed at Zone granularity - a Unit rollup passes null since it may span zones with different (or no) tank calibration, and TankFillPercent/VolumeLiters stay null there.
        private static SensorAverages Average(IReadOnlyCollection<UnitZoneDeviceSnapshot> snapshots, DeviceFarmUnitZoneRow? zone = null)
        {
            double? waterLevel = snapshots.Select(s => s.WaterLevel).Average();
            // Fill fraction is linear, so averaging raw WaterLevel first and calibrating once is equivalent to calibrating per device then averaging.
            (double? tankFillPercent, double? tankVolumeLiters) = zone == null
                ? (null, null)
                : TankCalculator.Compute(waterLevel, zone.WaterLevelRawEmpty, zone.WaterLevelRawFull, zone.TankCapacityLiters);

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
                WaterLevel = waterLevel,
                Wind = snapshots.Select(s => s.Wind).Average(),
                Ec = snapshots.Select(s => s.Ec).Average(),
                Weight = snapshots.Select(s => s.Weight).Average(),
                TankFillPercent = tankFillPercent,
                TankVolumeLiters = tankVolumeLiters,
            };
        }

        /// "Currently active" per alert type, reusing each evaluator's own existing dedup state instead of a new persistence layer: Device.OfflineNotifiedAt/LowBatteryNotifiedAt, DeviceFarmUnitZoneRow.TankRefillNotifiedAt, TenantWeatherStateRow.FrostPredicted. RuleTriggered/Satellite* have no such continuous state and are rejected by the caller before this is reached.
        public async Task<bool> DashboardAlertStatusGetAsync(HierarchyNodeKind level, int levelId, NotificationEventType eventType)
        {
            if (eventType == NotificationEventType.Frost)
            {
                int? tenantId = level switch
                {
                    HierarchyNodeKind.Farm => await db.DeviceFarms.AsNoTracking().Where(f => f.IDDeviceFarm == levelId).Select(f => f.TenantID).FirstOrDefaultAsync(),
                    HierarchyNodeKind.Unit => await db.DeviceFarmUnits.AsNoTracking().Where(u => u.IDDeviceFarmUnit == levelId).Select(u => u.TenantID).FirstOrDefaultAsync(),
                    HierarchyNodeKind.Zone => await db.DeviceFarmUnitZones.AsNoTracking().Where(z => z.IDDeviceFarmUnitZone == levelId).Select(z => z.TenantID).FirstOrDefaultAsync(),
                    _ => null,
                };
                return tenantId is int tid && await db.TenantWeatherStates.AsNoTracking().Where(t => t.TenantID == tid).Select(t => t.FrostPredicted).FirstOrDefaultAsync();
            }

            List<int> unitIds = level switch
            {
                HierarchyNodeKind.Farm => await db.DeviceFarmUnits.AsNoTracking().Where(u => u.DeviceFarmID == levelId).Select(u => u.IDDeviceFarmUnit).ToListAsync(),
                HierarchyNodeKind.Unit => [levelId],
                _ => [],
            };

            if (eventType == NotificationEventType.TankRefill)
            {
                List<int> zoneIds = level switch
                {
                    HierarchyNodeKind.Zone => [levelId],
                    HierarchyNodeKind.Unit or HierarchyNodeKind.Farm => await db.DeviceFarmUnitZones.AsNoTracking().Where(z => unitIds.Contains(z.DeviceFarmUnitID)).Select(z => z.IDDeviceFarmUnitZone).ToListAsync(),
                    _ => [],
                };
                return await db.DeviceFarmUnitZones.AsNoTracking().AnyAsync(z => zoneIds.Contains(z.IDDeviceFarmUnitZone) && z.TankRefillNotifiedAt != null);
            }

            IQueryable<int> scopedDeviceIds = level switch
            {
                HierarchyNodeKind.Zone => db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitZoneID == levelId).Select(d => d.IDDevice),
                HierarchyNodeKind.Unit or HierarchyNodeKind.Farm => db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitID != null && unitIds.Contains(d.DeviceFarmUnitID.Value)).Select(d => d.IDDevice),
                _ => Enumerable.Empty<int>().AsQueryable(),
            };
            // OfflineNotifiedAt/LowBatteryNotifiedAt live on DeviceDiagnosticRow (1:1 with device via DeviceID), not DeviceRow itself.
            return eventType switch
            {
                NotificationEventType.Offline => await db.DeviceDiagnostics.AsNoTracking().AnyAsync(x => scopedDeviceIds.Contains(x.DeviceID) && x.OfflineNotifiedAt != null),
                NotificationEventType.LowBattery => await db.DeviceDiagnostics.AsNoTracking().AnyAsync(x => scopedDeviceIds.Contains(x.DeviceID) && x.LowBatteryNotifiedAt != null),
                _ => false,
            };
        }
    }
}
