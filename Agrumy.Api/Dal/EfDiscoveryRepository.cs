using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IDiscoveryRepository - reads db.Devices directly for scanner scoping rather than calling into IDeviceRepository, but that's a direct DbSet read, not a facet-interface dependency.
    internal sealed class EfDiscoveryRepository(AgrumyDbContext db) : IDiscoveryRepository
    {
        public async Task DiscoveryReportAddAsync(int scanningDeviceId, string discoveredApMac, int? rssi)
        {
            db.DeviceDiscoveryReports.Add(new DeviceDiscoveryReportRow
            {
                ScanningDeviceID = scanningDeviceId,
                DiscoveredApMac = discoveredApMac,
                Rssi = rssi,
                DateReported = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        public async Task<IList<DiscoveryResult>> DiscoveryResultsGetAsync(int? tenantId, int? unitId, int? zoneId, int? parcelId = null)
        {
            IQueryable<DeviceRow> scanners = db.Devices.AsNoTracking();
            if (zoneId is int zid)
            {
                scanners = scanners.Where(d => d.DeviceFarmUnitZoneID == zid);
            }
            else if (parcelId is int pid)
            {
                scanners = scanners.Where(d => d.FarmOpenfieldCropParcelID == pid);
            }
            else if (unitId is int uid)
            {
                scanners = scanners.Where(d => d.DeviceFarmUnitID == uid);
            }
            else if (tenantId != null)
            {
                scanners = scanners.Where(d => d.TenantID == tenantId);
            }

            var reports = await (
                from r in db.DeviceDiscoveryReports.AsNoTracking()
                join d in scanners on r.ScanningDeviceID equals d.IDDevice
                select new DiscoveryResult
                {
                    DiscoveredApMac = r.DiscoveredApMac,
                    Rssi = r.Rssi,
                    ScanningDeviceID = r.ScanningDeviceID,
                    ScanningDeviceName = d.DeviceName,
                    TenantID = d.TenantID,
                    DateReported = r.DateReported,
                }).ToListAsync();

            return DiscoveryResultPicker.Pick(reports);
        }

        public async Task<DiscoveryResult?> DiscoveryResultGetAsync(string discoveredApMac, int? tenantId)
        {
            IQueryable<DeviceRow> scanners = db.Devices.AsNoTracking();
            if (tenantId != null)
            {
                scanners = scanners.Where(d => d.TenantID == tenantId);
            }

            var reports = await (
                from r in db.DeviceDiscoveryReports.AsNoTracking()
                where r.DiscoveredApMac == discoveredApMac
                join d in scanners on r.ScanningDeviceID equals d.IDDevice
                select new DiscoveryResult
                {
                    DiscoveredApMac = r.DiscoveredApMac,
                    Rssi = r.Rssi,
                    ScanningDeviceID = r.ScanningDeviceID,
                    ScanningDeviceName = d.DeviceName,
                    TenantID = d.TenantID,
                    DateReported = r.DateReported,
                }).ToListAsync();

            return DiscoveryResultPicker.Pick(reports).SingleOrDefault();
        }
    }
}
