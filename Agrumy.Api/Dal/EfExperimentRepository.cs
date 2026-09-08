using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IExperimentRepository, extracted as its own facet from the start (unlike Simulation's original single-file history) - a leaf, no dependency on any other facet.
    internal sealed class EfExperimentRepository(AgrumyDbContext db) : IExperimentRepository
    {
        public async Task<Experiment> ExperimentAddAsync(Experiment experiment)
        {
            var row = new ExperimentRow
            {
                TenantID = experiment.TenantID ?? 0,
                Name = experiment.Name,
                Scope = (int)experiment.Scope,
                ScopeID = experiment.ScopeID,
                StartedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = experiment.ExpiresAtUtc,
            };
            db.Experiments.Add(row);
            await db.SaveChangesAsync();
            return await WithScopeNameAsync(row);
        }

        public async Task<IList<Experiment>> ExperimentsGetAsync(int? tenantID)
        {
            IQueryable<ExperimentRow> q = db.Experiments.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(e => e.TenantID == tenantID);
            }
            List<ExperimentRow> rows = await q.OrderByDescending(e => e.StartedAtUtc).ToListAsync();
            var result = new List<Experiment>();
            foreach (ExperimentRow row in rows)
            {
                result.Add(await WithScopeNameAsync(row));
            }
            return result;
        }

        public async Task<Experiment?> ExperimentGetByIdAsync(int idExperiment)
        {
            ExperimentRow? row = await db.Experiments.AsNoTracking().FirstOrDefaultAsync(e => e.IDExperiment == idExperiment);
            return row == null ? null : await WithScopeNameAsync(row);
        }

        public async Task ExperimentStopAsync(int idExperiment)
        {
            await db.Experiments.Where(e => e.IDExperiment == idExperiment && e.StoppedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.StoppedAtUtc, DateTimeOffset.UtcNow));
        }

        public async Task<int?> ActiveExperimentIdForZoneAsync(int idDeviceFarmUnitZone)
        {
            if (await ActiveExperimentIdForScopeAsync(ExperimentScope.Zone, idDeviceFarmUnitZone) is int zoneExperimentId)
            {
                return zoneExperimentId;
            }
            DeviceFarmUnitZoneRow? zone = await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            if (zone == null)
            {
                return null;
            }
            if (await ActiveExperimentIdForScopeAsync(ExperimentScope.Unit, zone.DeviceFarmUnitID) is int unitExperimentId)
            {
                return unitExperimentId;
            }
            DeviceFarmUnitRow? unit = await db.DeviceFarmUnits.AsNoTracking().FirstOrDefaultAsync(u => u.IDDeviceFarmUnit == zone.DeviceFarmUnitID);
            return unit?.DeviceFarmID is int idFarm ? await ActiveExperimentIdForScopeAsync(ExperimentScope.Farm, idFarm) : null;
        }

        private async Task<int?> ActiveExperimentIdForScopeAsync(ExperimentScope scope, int scopeId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return await db.Experiments.AsNoTracking()
                .Where(e => e.Scope == (int)scope && e.ScopeID == scopeId && e.StoppedAtUtc == null && (e.ExpiresAtUtc == null || e.ExpiresAtUtc > now))
                .Select(e => (int?)e.IDExperiment)
                .FirstOrDefaultAsync();
        }

        public async Task<IDictionary<int, int>> ActiveExperimentIdsByZoneAsync(int tenantID)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            List<ExperimentRow> active = await db.Experiments.AsNoTracking()
                .Where(e => e.TenantID == tenantID && e.StoppedAtUtc == null && (e.ExpiresAtUtc == null || e.ExpiresAtUtc > now))
                .ToListAsync();
            var map = new Dictionary<int, int>();
            if (active.Count == 0)
            {
                return map;
            }

            Dictionary<int, int> zoneScoped = active.Where(e => (ExperimentScope)e.Scope == ExperimentScope.Zone).ToDictionary(e => e.ScopeID, e => e.IDExperiment);
            Dictionary<int, int> unitScoped = active.Where(e => (ExperimentScope)e.Scope == ExperimentScope.Unit).ToDictionary(e => e.ScopeID, e => e.IDExperiment);
            Dictionary<int, int> farmScoped = active.Where(e => (ExperimentScope)e.Scope == ExperimentScope.Farm).ToDictionary(e => e.ScopeID, e => e.IDExperiment);

            List<DeviceFarmUnitZoneRow> zones = await db.DeviceFarmUnitZones.AsNoTracking().Where(z => z.TenantID == tenantID).ToListAsync();
            Dictionary<int, int?> unitFarmById = await db.DeviceFarmUnits.AsNoTracking().Where(u => u.TenantID == tenantID)
                .ToDictionaryAsync(u => u.IDDeviceFarmUnit, u => u.DeviceFarmID);

            foreach (DeviceFarmUnitZoneRow zone in zones)
            {
                if (zoneScoped.TryGetValue(zone.IDDeviceFarmUnitZone, out int zoneExperimentId))
                {
                    map[zone.IDDeviceFarmUnitZone] = zoneExperimentId;
                }
                else if (unitScoped.TryGetValue(zone.DeviceFarmUnitID, out int unitExperimentId))
                {
                    map[zone.IDDeviceFarmUnitZone] = unitExperimentId;
                }
                else if (unitFarmById.TryGetValue(zone.DeviceFarmUnitID, out int? idFarm) && idFarm is int farmId && farmScoped.TryGetValue(farmId, out int farmExperimentId))
                {
                    map[zone.IDDeviceFarmUnitZone] = farmExperimentId;
                }
            }
            return map;
        }

        public async Task SensorDataExperimentAddRangeAsync(int idExperiment, int deviceID, int tenantID, IReadOnlyList<SensorDataPushReading> readings)
        {
            if (readings.Count == 0)
            {
                return;
            }
            db.SensorDataExperiments.AddRange(readings.Select(r => new SensorDataExperimentRow
            {
                IDExperiment = idExperiment,
                TenantID = tenantID,
                DeviceID = deviceID,
                Temperature = r.Temperature,
                SoilTemperature = r.SoilTemperature,
                Humidity = r.Humidity,
                Moisture = r.Moisture,
                Light = r.Light,
                Co2 = r.Co2,
                Tvoc = r.Tvoc,
                Barometer = r.Barometer,
                LiquidPH = r.LiquidPH,
                RainLevel = r.RainLevel,
                WaterLevel = r.WaterLevel,
                Wind = r.Wind,
                DateCreated = DateTimeOffset.UtcNow,
            }));
            await db.SaveChangesAsync();
        }

        public async Task ControllerDataExperimentAddRangeAsync(int idExperiment, int deviceID, int tenantID, IList<ControllerDataPush> entries)
        {
            if (entries.Count == 0)
            {
                return;
            }
            db.ControllerDataExperiments.AddRange(entries.Select(entry => new ControllerDataExperimentRow
            {
                IDExperiment = idExperiment,
                TenantID = tenantID,
                DeviceID = deviceID,
                RelayFunction = (int)entry.RelayFunction,
                IsOn = entry.IsOn,
                Percent = entry.Percent,
                DateCreated = entry.DateCreated ?? DateTimeOffset.UtcNow,
            }));
            await db.SaveChangesAsync();
        }

        public async Task<IList<ExperimentSensorSample>> ExperimentSensorSamplesGetAsync(int idExperiment, int limit) =>
            await (from s in db.SensorDataExperiments.AsNoTracking()
                   join d in db.Devices.AsNoTracking() on s.DeviceID equals d.IDDevice into deviceJoin
                   from d in deviceJoin.DefaultIfEmpty()
                   where s.IDExperiment == idExperiment
                   orderby s.DateCreated descending
                   select new ExperimentSensorSample
                   {
                       DeviceID = s.DeviceID,
                       DeviceName = d != null ? d.DeviceName : null,
                       Temperature = s.Temperature,
                       SoilTemperature = s.SoilTemperature,
                       Humidity = s.Humidity,
                       Moisture = s.Moisture,
                       Light = s.Light,
                       Co2 = s.Co2,
                       Tvoc = s.Tvoc,
                       Barometer = s.Barometer,
                       LiquidPH = s.LiquidPH,
                       RainLevel = s.RainLevel,
                       WaterLevel = s.WaterLevel,
                       Wind = s.Wind,
                       DateCreated = s.DateCreated,
                   }).Take(limit).ToListAsync();

        public async Task<IList<ExperimentControllerEvent>> ExperimentControllerEventsGetAsync(int idExperiment, int limit) =>
            await (from c in db.ControllerDataExperiments.AsNoTracking()
                   join d in db.Devices.AsNoTracking() on c.DeviceID equals d.IDDevice into deviceJoin
                   from d in deviceJoin.DefaultIfEmpty()
                   where c.IDExperiment == idExperiment
                   orderby c.DateCreated descending
                   select new ExperimentControllerEvent
                   {
                       DeviceID = c.DeviceID,
                       DeviceName = d != null ? d.DeviceName : null,
                       RelayFunction = (RelayFunction)c.RelayFunction,
                       IsOn = c.IsOn,
                       Percent = c.Percent,
                       DateCreated = c.DateCreated,
                   }).Take(limit).ToListAsync();

        private async Task<Experiment> WithScopeNameAsync(ExperimentRow row)
        {
            var scope = (ExperimentScope)row.Scope;
            string? scopeName = scope switch
            {
                ExperimentScope.Farm => (await db.DeviceFarms.AsNoTracking().FirstOrDefaultAsync(f => f.IDDeviceFarm == row.ScopeID))?.DeviceFarmName,
                ExperimentScope.Unit => (await db.DeviceFarmUnits.AsNoTracking().FirstOrDefaultAsync(u => u.IDDeviceFarmUnit == row.ScopeID))?.DeviceFarmUnitName,
                _ => (await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == row.ScopeID))?.DeviceFarmUnitZoneName,
            };
            return new Experiment
            {
                IDExperiment = row.IDExperiment,
                TenantID = row.TenantID,
                Name = row.Name,
                Scope = scope,
                ScopeID = row.ScopeID,
                ScopeName = scopeName,
                StartedAtUtc = row.StartedAtUtc,
                ExpiresAtUtc = row.ExpiresAtUtc,
                StoppedAtUtc = row.StoppedAtUtc,
            };
        }
    }
}
