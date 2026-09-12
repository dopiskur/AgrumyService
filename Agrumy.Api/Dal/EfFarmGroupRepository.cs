using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IFarmGroupRepository - see its own doc comment.
    internal sealed class EfFarmGroupRepository(AgrumyDbContext db) : IFarmGroupRepository
    {
        public async Task<IList<FarmGroup>> FarmGroupsGetAsync(int? tenantID)
        {
            IQueryable<FarmGroupRow> q = db.FarmGroups.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(g => g.TenantID == tenantID);
            }
            var rows = await q.OrderBy(g => g.Name).ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<FarmGroup?> FarmGroupGetByIdAsync(int idFarmGroup)
        {
            var row = await db.FarmGroups.AsNoTracking().FirstOrDefaultAsync(g => g.IDFarmGroup == idFarmGroup);
            return row == null ? null : ToDto(row);
        }

        public async Task<FarmGroup> FarmGroupCreateAsync(string? name, int? tenantID)
        {
            var row = new FarmGroupRow { TenantID = tenantID, Name = name };
            db.FarmGroups.Add(row);
            await db.SaveChangesAsync();
            return ToDto(row);
        }

        public async Task FarmGroupDeleteAsync(int idFarmGroup)
        {
            // Soft-delete is a plain UPDATE, not a real DELETE - the FK's SetNull behavior never fires, so member farms are detached explicitly here.
            await db.DeviceFarms.Where(f => f.FarmGroupID == idFarmGroup).ExecuteUpdateAsync(set => set.SetProperty(f => f.FarmGroupID, (int?)null));
            await db.FarmGroups.Where(g => g.IDFarmGroup == idFarmGroup)
                .ExecuteUpdateAsync(set => set.SetProperty(g => g.Deleted, true).SetProperty(g => g.DeletedAtUtc, DateTimeOffset.UtcNow));
        }

        public async Task FarmAssignToGroupAsync(int idFarm, int? idFarmGroup) =>
            await db.DeviceFarms.Where(f => f.IDDeviceFarm == idFarm).ExecuteUpdateAsync(set => set.SetProperty(f => f.FarmGroupID, idFarmGroup));

        private static FarmGroup ToDto(FarmGroupRow g) => new()
        {
            IDFarmGroup = g.IDFarmGroup,
            TenantID = g.TenantID,
            Name = g.Name,
            DeletedAtUtc = g.DeletedAtUtc,
        };
    }
}
