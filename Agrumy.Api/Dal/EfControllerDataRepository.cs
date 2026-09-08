using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IControllerDataRepository, extracted out of the EfRepository god class - a leaf facet, its only cross-facet dependency being the experiment dual-write below. See Agrumy.Dal.Entities.ControllerDataRow for why this upserts current state rather than appending a log.
    internal sealed class EfControllerDataRepository(AgrumyDbContext db, IExperimentRepository experimentRepository) : IControllerDataRepository
    {
        public async Task ControllerDataPushAsync(int deviceID, int tenantID, IList<ControllerDataPush> entries)
        {
            // Mirrors this same push into the append-only dataControllerExperiment log when the device's zone is currently under an active experiment; a single zone lookup covers the whole batch, not one per entry.
            int? idZone = await db.Devices.AsNoTracking().Where(d => d.IDDevice == deviceID).Select(d => d.DeviceFarmUnitZoneID).FirstOrDefaultAsync();
            if (idZone is int zone && await experimentRepository.ActiveExperimentIdForZoneAsync(zone) is int idExperiment)
            {
                await experimentRepository.ControllerDataExperimentAddRangeAsync(idExperiment, deviceID, tenantID, entries);
            }

            foreach (ControllerDataPush entry in entries)
            {
                var row = await db.ControllerData.FirstOrDefaultAsync(c => c.DeviceID == deviceID && c.RelayFunction == (int)entry.RelayFunction);
                if (row == null)
                {
                    row = new ControllerDataRow { DeviceID = deviceID, RelayFunction = (int)entry.RelayFunction };
                    db.ControllerData.Add(row);
                }
                row.TenantID = tenantID;
                row.IsOn = entry.IsOn;
                row.DateChanged = entry.DateCreated ?? DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }

        public async Task<IList<ControllerDataStatus>> ControllerDataGetAsync(int deviceID)
        {
            return await db.ControllerData.AsNoTracking()
                .Where(c => c.DeviceID == deviceID)
                .Select(c => new ControllerDataStatus { RelayFunction = (RelayFunction)c.RelayFunction, IsOn = c.IsOn, DateChanged = c.DateChanged })
                .ToListAsync();
        }
    }
}
