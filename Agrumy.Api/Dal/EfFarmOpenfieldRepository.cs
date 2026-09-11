using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IFarmOpenfieldRepository - see its own doc comment. Crop/Parcel CRUD moved to EfSowingRepository/EfFarmParcelRepository/EfCropCatalogRepository (restructure R).
    internal sealed class EfFarmOpenfieldRepository(AgrumyDbContext db) : IFarmOpenfieldRepository
    {
        public Task<(DeviceFarm Farm, FarmOpenfield Openfield)> FarmOpenfieldCreateAsync(string? farmName, int? tenantID, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                // Same next-DisplayOrder + insert shape as EfDeviceFarmUnitRepository.DeviceFarmAddAsync - duplicated rather than called into, to avoid a two-way dependency between the two repositories.
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

        private static FarmOpenfield ToDtoOpenfield(FarmOpenfieldRow o) => new()
        {
            IDFarmOpenfield = o.IDFarmOpenfield,
            TenantID = o.TenantID,
            FarmID = o.FarmID,
            DeletedAtUtc = o.DeletedAtUtc,
        };
    }
}
