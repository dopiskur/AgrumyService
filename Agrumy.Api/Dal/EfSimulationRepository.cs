using api.Dal.Entities;
using api.Dal.Interface;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Dal
{
    /// ISimulationRepository, extracted out of the EfRepository god class (roadmap #246) - the virtual-device registry plus simulation sessions (#403). VirtualDeviceDeleteAsync needs IDeviceRepository (delegates the actual device-row delete to it), an already-extracted facet, so no circular dependency.
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

        // ---- Simulation sessions (roadmap #403) ----------------------------

        /// Roadmap #414 (2) - name only; StartedAtUtc/ExpiresAtUtc stay null until SimulationSessionStartAsync.
        public async Task<SimulationSession> SimulationSessionAddAsync(SimulationSession session)
        {
            var row = new SimulationSessionRow
            {
                TenantID = session.TenantID ?? 0,
                Name = session.Name,
            };
            db.SimulationSessions.Add(row);
            await db.SaveChangesAsync();
            return ToDtoSession(row);
        }

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

        /// Roadmap #414 (1) - turns off every member physical device's sensor override first (same cleanup StopSession does), same reasoning: a deleted session must not leave a physical device stuck simulating forever.
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
