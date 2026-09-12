using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// ISimulationRepository - the virtual-device registry plus simulation sessions. VirtualDeviceDeleteAsync needs IDeviceRepository (delegates the actual device-row delete to it), an already-extracted facet, so no circular dependency.
    internal sealed class EfSimulationRepository(AgrumyDbContext db, IDeviceRepository deviceRepository) : ISimulationRepository
    {
        public async Task VirtualDeviceRegisterAsync(int deviceID)
        {
            db.DeviceVirtuals.Add(new DeviceVirtualRow { DeviceID = deviceID, DateCreated = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        public async Task<IList<int>> VirtualDeviceIdsGetAsync()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return await db.DeviceVirtuals.AsNoTracking()
                .Join(db.SimulationSessionDevices.AsNoTracking(), v => v.DeviceID, sd => sd.DeviceID, (v, sd) => sd)
                .Join(db.SimulationSessions.AsNoTracking(), sd => sd.IDSimulationSession, s => s.IDSimulationSession, (sd, s) => new { sd.DeviceID, s.StoppedAtUtc, s.ExpiresAtUtc })
                .Where(x => x.StoppedAtUtc == null && x.ExpiresAtUtc > now)
                .Select(x => x.DeviceID)
                .Distinct()
                .ToListAsync();
        }

        public async Task<IList<int>> VirtualDeviceIdsGetAsync(int? tenantID)
        {
            IQueryable<int> ids = db.DeviceVirtuals.AsNoTracking()
                .Join(db.Devices.AsNoTracking(), v => v.DeviceID, d => d.IDDevice, (v, d) => new { v.DeviceID, d.TenantID })
                .Where(x => tenantID == null || x.TenantID == tenantID)
                .Select(x => x.DeviceID);
            return await ids.ToListAsync();
        }

        public async Task VirtualDeviceDeleteAsync(int deviceID, int? tenantID)
        {
            // Synthetic telemetry has no historical value once the device is gone - unlike DeviceDeleteAsync's rule for a REAL device, whose sensorData stays for the record.
            await db.SensorData.Where(s => s.DeviceID == deviceID).ExecuteDeleteAsync();
            await db.SimulationSessionDevices.Where(sd => sd.DeviceID == deviceID).ExecuteDeleteAsync();
            await db.DeviceVirtuals.Where(v => v.DeviceID == deviceID).ExecuteDeleteAsync();
            await deviceRepository.DeviceDeleteAsync(deviceID, tenantID);
        }

        // ---- Simulation sessions ----------------------------

        /// Name only; StartedAtUtc/ExpiresAtUtc stay null until SimulationSessionStartAsync.
        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        public Task<SimulationSession> SimulationSessionAddAsync(SimulationSession session, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                var row = new SimulationSessionRow
                {
                    TenantID = session.TenantID ?? 0,
                    Name = session.Name,
                };
                db.SimulationSessions.Add(row);
                await db.SaveChangesAsync();
                return ToDtoSession(row);
            });

        /// Same effect whether this is the session's first start or a later Resume after a Stop - sets a fresh window from now, clearing any previous stop.
        public async Task SimulationSessionStartAsync(int idSimulationSession, int durationMinutes)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await db.SimulationSessions.Where(s => s.IDSimulationSession == idSimulationSession)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.StartedAtUtc, now)
                    .SetProperty(s => s.ExpiresAtUtc, now.AddMinutes(durationMinutes))
                    .SetProperty(s => s.StoppedAtUtc, (DateTimeOffset?)null));
        }

        /// Turns off every member physical device's sensor override first (same cleanup StopSession does), same reasoning: a deleted session must not leave a physical device stuck simulating forever.
        public async Task SimulationSessionDeleteAsync(int idSimulationSession)
        {
            await db.SimulationSessionDevices.Where(sd => sd.IDSimulationSession == idSimulationSession).ExecuteDeleteAsync();
            await db.SimulationSessions.Where(s => s.IDSimulationSession == idSimulationSession).ExecuteDeleteAsync();
        }

        public async Task<IList<SimulationSession>> SimulationSessionsGetAsync(int? tenantID)
        {
            IQueryable<SimulationSessionRow> q = db.SimulationSessions.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(s => s.TenantID == tenantID);
            }
            var rows = await q.OrderByDescending(s => s.StartedAtUtc).ToListAsync();
            return rows.Select(ToDtoSession).ToList();
        }

        public async Task<SimulationSession?> SimulationSessionGetByIdAsync(int idSimulationSession)
        {
            SimulationSessionRow? row = await db.SimulationSessions.AsNoTracking().FirstOrDefaultAsync(s => s.IDSimulationSession == idSimulationSession);
            if (row == null)
            {
                return null;
            }
            SimulationSession dto = ToDtoSession(row);
            List<int> deviceIds = await db.SimulationSessionDevices.AsNoTracking()
                .Where(sd => sd.IDSimulationSession == idSimulationSession).Select(sd => sd.DeviceID).ToListAsync();
            List<DeviceRow> deviceRows = await db.Devices.AsNoTracking().Where(d => deviceIds.Contains(d.IDDevice)).ToListAsync();
            dto.Devices = deviceRows.Select(EfDeviceRepository.ToDto).Select(d => d.ToDto()).ToList();
            return dto;
        }

        public async Task SimulationSessionStopAsync(int idSimulationSession)
        {
            // First write wins - an explicit stop and the expiry evaluator's own stop must not overwrite whichever timestamp landed first.
            await db.SimulationSessions.Where(s => s.IDSimulationSession == idSimulationSession && s.StoppedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.StoppedAtUtc, DateTimeOffset.UtcNow));
        }

        public async Task<bool> SimulationSessionDeviceAddAsync(int idSimulationSession, int deviceID)
        {
            if (await DeviceActiveSimulationSessionIdGetAsync(deviceID) is int existingId && existingId != idSimulationSession)
            {
                return false;
            }
            bool alreadyMember = await db.SimulationSessionDevices.AsNoTracking()
                .AnyAsync(sd => sd.IDSimulationSession == idSimulationSession && sd.DeviceID == deviceID);
            if (alreadyMember)
            {
                return true; // idempotent - adding a device already in THIS session is a no-op success, not a conflict
            }
            db.SimulationSessionDevices.Add(new SimulationSessionDeviceRow { IDSimulationSession = idSimulationSession, DeviceID = deviceID });
            await db.SaveChangesAsync();
            return true;
        }

        public async Task SimulationSessionDeviceRemoveAsync(int idSimulationSession, int deviceID) =>
            await db.SimulationSessionDevices.Where(sd => sd.IDSimulationSession == idSimulationSession && sd.DeviceID == deviceID).ExecuteDeleteAsync();

        // ---- Simulation groups - a whole Unit/Zone added together, one override value set fanned out to every member device's own DeviceSimulation. ----

        public async Task<SimulationGroup> SimulationGroupAddAsync(SimulationGroup group)
        {
            var row = ToRowGroup(group);
            db.SimulationGroups.Add(row);
            await db.SaveChangesAsync();

            List<int> candidateIds = group.Scope switch
            {
                HierarchyNodeKind.Unit => await db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitID == group.ScopeID).Select(d => d.IDDevice).ToListAsync(),
                HierarchyNodeKind.Zone => await db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitZoneID == group.ScopeID).Select(d => d.IDDevice).ToListAsync(),
                HierarchyNodeKind.Sowing => await db.Devices.AsNoTracking().Where(d => d.SowingID == group.ScopeID).Select(d => d.IDDevice).ToListAsync(),
                HierarchyNodeKind.FarmParcelZone => await db.Devices.AsNoTracking().Where(d => d.FarmParcelZoneID == group.ScopeID).Select(d => d.IDDevice).ToListAsync(),
                _ => throw new ArgumentOutOfRangeException(nameof(group), group.Scope, "Unknown simulation group scope"),
            };

            DeviceSimulation overrideValues = ToDeviceSimulation(group);
            foreach (int deviceId in candidateIds)
            {
                // Already busy with a DIFFERENT active session - skip it, don't fail the whole group over one device someone else is already simulating.
                if (await DeviceActiveSimulationSessionIdGetAsync(deviceId) is int existingId && existingId != row.IDSimulationSession)
                {
                    continue;
                }
                bool alreadyMember = await db.SimulationSessionDevices.AsNoTracking()
                    .AnyAsync(sd => sd.IDSimulationSession == row.IDSimulationSession && sd.DeviceID == deviceId);
                if (alreadyMember)
                {
                    // Was added individually (or by a different group) earlier - claim it for THIS group now, so editing/removing the group also covers it going forward.
                    await db.SimulationSessionDevices.Where(sd => sd.IDSimulationSession == row.IDSimulationSession && sd.DeviceID == deviceId)
                        .ExecuteUpdateAsync(s => s.SetProperty(sd => sd.IDSimulationGroup, row.IDSimulationGroup));
                }
                else
                {
                    db.SimulationSessionDevices.Add(new SimulationSessionDeviceRow { IDSimulationSession = row.IDSimulationSession, DeviceID = deviceId, IDSimulationGroup = row.IDSimulationGroup });
                }
                if (!await db.DeviceVirtuals.AsNoTracking().AnyAsync(v => v.DeviceID == deviceId))
                {
                    await deviceRepository.DeviceSimulationSetAsync(deviceId, overrideValues);
                }
            }
            await db.SaveChangesAsync();

            return await WithNameAndCountAsync(row);
        }

        public async Task<IList<SimulationGroup>> SimulationGroupsGetAsync(int idSimulationSession)
        {
            var rows = await db.SimulationGroups.AsNoTracking().Where(g => g.IDSimulationSession == idSimulationSession).ToListAsync();
            var result = new List<SimulationGroup>();
            foreach (var row in rows)
            {
                result.Add(await WithNameAndCountAsync(row));
            }
            return result;
        }

        public async Task<SimulationGroup?> SimulationGroupGetByIdAsync(int idSimulationGroup)
        {
            var row = await db.SimulationGroups.AsNoTracking().FirstOrDefaultAsync(g => g.IDSimulationGroup == idSimulationGroup);
            return row == null ? null : await WithNameAndCountAsync(row);
        }

        /// Re-applies the new override values to whichever devices currently belong to this group - does NOT re-resolve Unit/Zone membership, same "snapshot at add time" convention as a single device's own add.
        public async Task SimulationGroupUpdateAsync(SimulationGroup group)
        {
            int idGroup = group.IDSimulationGroup!.Value;
            var row = ToRowGroup(group);
            await db.SimulationGroups.Where(g => g.IDSimulationGroup == idGroup)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.Temperature, row.Temperature).SetProperty(g => g.SoilTemperature, row.SoilTemperature)
                    .SetProperty(g => g.Humidity, row.Humidity).SetProperty(g => g.Battery, row.Battery)
                    .SetProperty(g => g.Moisture, row.Moisture).SetProperty(g => g.Light, row.Light)
                    .SetProperty(g => g.Co2, row.Co2).SetProperty(g => g.Tvoc, row.Tvoc)
                    .SetProperty(g => g.Barometer, row.Barometer).SetProperty(g => g.LiquidPH, row.LiquidPH)
                    .SetProperty(g => g.RainLevel, row.RainLevel).SetProperty(g => g.WaterLevel, row.WaterLevel)
                    .SetProperty(g => g.Wind, row.Wind));

            List<int> memberIds = await db.SimulationSessionDevices.AsNoTracking().Where(sd => sd.IDSimulationGroup == idGroup).Select(sd => sd.DeviceID).ToListAsync();
            DeviceSimulation overrideValues = ToDeviceSimulation(group);
            foreach (int deviceId in memberIds)
            {
                if (!await db.DeviceVirtuals.AsNoTracking().AnyAsync(v => v.DeviceID == deviceId))
                {
                    await deviceRepository.DeviceSimulationSetAsync(deviceId, overrideValues);
                }
            }
        }

        /// Turns off every physical member's override (same cleanup a single device's own removal does) before dropping the membership rows and the group itself - a removed group must not leave a device stuck simulating.
        public async Task SimulationGroupDeleteAsync(int idSimulationGroup)
        {
            List<int> memberIds = await db.SimulationSessionDevices.AsNoTracking().Where(sd => sd.IDSimulationGroup == idSimulationGroup).Select(sd => sd.DeviceID).ToListAsync();
            foreach (int deviceId in memberIds)
            {
                if (!await db.DeviceVirtuals.AsNoTracking().AnyAsync(v => v.DeviceID == deviceId))
                {
                    await deviceRepository.DeviceSimulationSetAsync(deviceId, new DeviceSimulation { Enabled = false });
                }
            }
            await db.SimulationSessionDevices.Where(sd => sd.IDSimulationGroup == idSimulationGroup).ExecuteDeleteAsync();
            await db.SimulationGroups.Where(g => g.IDSimulationGroup == idSimulationGroup).ExecuteDeleteAsync();
        }

        private async Task<SimulationGroup> WithNameAndCountAsync(SimulationGroupRow row)
        {
            SimulationGroup dto = ToDtoGroup(row);
            dto.MemberDeviceCount = await db.SimulationSessionDevices.AsNoTracking().CountAsync(sd => sd.IDSimulationGroup == row.IDSimulationGroup);
            dto.ScopeName = (HierarchyNodeKind)row.Scope switch
            {
                HierarchyNodeKind.Unit => (await db.DeviceFarmUnits.AsNoTracking().FirstOrDefaultAsync(u => u.IDDeviceFarmUnit == row.ScopeID))?.DeviceFarmUnitName,
                // Sowing has no free-text name of its own (restructure R) - display its catalog crop's name, plus variety when set.
                HierarchyNodeKind.Sowing => await SowingDisplayNameAsync(row.ScopeID),
                HierarchyNodeKind.FarmParcelZone => (await db.FarmParcelZones.AsNoTracking().FirstOrDefaultAsync(p => p.IDFarmParcelZone == row.ScopeID))?.FarmParcelZoneName,
                _ => (await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == row.ScopeID))?.DeviceFarmUnitZoneName,
            };
            return dto;
        }

        private async Task<string?> SowingDisplayNameAsync(int idSowing)
        {
            var row = await db.Sowings.AsNoTracking()
                .Where(s => s.IDSowing == idSowing)
                .Select(s => new { s.Variety, CropName = db.Crops.Where(c => c.IDCrop == s.CropID).Select(c => c.Name).FirstOrDefault() })
                .FirstOrDefaultAsync();
            if (row == null)
            {
                return null;
            }
            return string.IsNullOrEmpty(row.Variety) ? row.CropName : $"{row.CropName} ({row.Variety})";
        }

        private static SimulationGroupRow ToRowGroup(SimulationGroup g) => new()
        {
            IDSimulationGroup = g.IDSimulationGroup ?? 0,
            IDSimulationSession = g.IDSimulationSession!.Value,
            Scope = (int)g.Scope,
            ScopeID = g.ScopeID,
            Temperature = g.Temperature,
            SoilTemperature = g.SoilTemperature,
            Humidity = g.Humidity,
            Battery = g.Battery,
            Moisture = g.Moisture,
            Light = g.Light,
            Co2 = g.Co2,
            Tvoc = g.Tvoc,
            Barometer = g.Barometer,
            LiquidPH = g.LiquidPH,
            RainLevel = g.RainLevel,
            WaterLevel = g.WaterLevel,
            Wind = g.Wind,
        };

        private static SimulationGroup ToDtoGroup(SimulationGroupRow r) => new()
        {
            IDSimulationGroup = r.IDSimulationGroup,
            IDSimulationSession = r.IDSimulationSession,
            Scope = (HierarchyNodeKind)r.Scope,
            ScopeID = r.ScopeID,
            Temperature = r.Temperature,
            SoilTemperature = r.SoilTemperature,
            Humidity = r.Humidity,
            Battery = r.Battery,
            Moisture = r.Moisture,
            Light = r.Light,
            Co2 = r.Co2,
            Tvoc = r.Tvoc,
            Barometer = r.Barometer,
            LiquidPH = r.LiquidPH,
            RainLevel = r.RainLevel,
            WaterLevel = r.WaterLevel,
            Wind = r.Wind,
        };

        private static DeviceSimulation ToDeviceSimulation(SimulationGroup g) => new()
        {
            Enabled = true,
            Temperature = g.Temperature,
            SoilTemperature = g.SoilTemperature,
            Humidity = g.Humidity,
            Battery = g.Battery,
            Moisture = g.Moisture,
            Light = g.Light,
            Co2 = g.Co2,
            Tvoc = g.Tvoc,
            Barometer = g.Barometer,
            LiquidPH = g.LiquidPH,
            RainLevel = g.RainLevel,
            WaterLevel = g.WaterLevel,
            Wind = g.Wind,
        };

        public async Task<int?> DeviceActiveSimulationSessionIdGetAsync(int deviceID)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return await db.SimulationSessionDevices.AsNoTracking()
                .Where(sd => sd.DeviceID == deviceID)
                .Join(db.SimulationSessions.AsNoTracking(), sd => sd.IDSimulationSession, s => s.IDSimulationSession, (sd, s) => s)
                .Where(s => s.StoppedAtUtc == null && s.ExpiresAtUtc > now)
                .Select(s => (int?)s.IDSimulationSession)
                .FirstOrDefaultAsync();
        }

        public async Task<IDictionary<int, int>> ActiveSimulationSessionIdsByZoneAsync(int tenantID)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var activeMembers = db.SimulationSessionDevices.AsNoTracking()
                .Join(db.SimulationSessions.AsNoTracking().Where(s => s.TenantID == tenantID && s.StoppedAtUtc == null && s.ExpiresAtUtc > now),
                    sd => sd.IDSimulationSession, s => s.IDSimulationSession, (sd, s) => new { sd.DeviceID, s.IDSimulationSession });

            var zoneRows = await activeMembers
                .Join(db.Devices.AsNoTracking().Where(d => d.DeviceFarmUnitZoneID != null),
                    ms => ms.DeviceID, d => d.IDDevice, (ms, d) => new { LeafID = d.DeviceFarmUnitZoneID!.Value, ms.IDSimulationSession })
                .ToListAsync();
            // Open-Field's Parcel is Zone's own equivalent leaf - same dictionary, same "last write wins on overlap" convention, so RuleNotificationEvaluator's single per-organization lookup covers both branches.
            var parcelRows = await activeMembers
                .Join(db.Devices.AsNoTracking().Where(d => d.FarmParcelZoneID != null),
                    ms => ms.DeviceID, d => d.IDDevice, (ms, d) => new { LeafID = d.FarmParcelZoneID!.Value, ms.IDSimulationSession })
                .ToListAsync();

            var map = new Dictionary<int, int>();
            foreach (var r in zoneRows)
            {
                map[r.LeafID] = r.IDSimulationSession;
            }
            foreach (var r in parcelRows)
            {
                map[r.LeafID] = r.IDSimulationSession;
            }
            return map;
        }

        public async Task<IList<SimulationSession>> SimulationSessionsExpiredButActiveGetAsync(DateTimeOffset nowUtc)
        {
            List<SimulationSessionRow> rows = await db.SimulationSessions.AsNoTracking()
                .Where(s => s.StoppedAtUtc == null && s.ExpiresAtUtc <= nowUtc)
                .ToListAsync();
            var result = new List<SimulationSession>();
            foreach (SimulationSessionRow row in rows)
            {
                SimulationSession dto = ToDtoSession(row);
                dto.Devices = (await db.SimulationSessionDevices.AsNoTracking()
                    .Where(sd => sd.IDSimulationSession == row.IDSimulationSession).Select(sd => sd.DeviceID).ToListAsync())
                    .Select(id => new DeviceDto { IDDevice = id }).ToList();
                result.Add(dto);
            }
            return result;
        }

        private static SimulationSession ToDtoSession(SimulationSessionRow r) => new()
        {
            IDSimulationSession = r.IDSimulationSession,
            TenantID = r.TenantID,
            Name = r.Name,
            StartedAtUtc = r.StartedAtUtc,
            ExpiresAtUtc = r.ExpiresAtUtc,
            StoppedAtUtc = r.StoppedAtUtc,
        };
    }
}
