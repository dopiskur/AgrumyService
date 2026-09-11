using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    internal sealed class EfCropCatalogRepository(AgrumyDbContext db) : ICropCatalogRepository
    {
        public async Task<IList<Crop>> CropsGetAsync(int? tenantID)
        {
            var rows = await db.Crops.AsNoTracking()
                .Where(c => c.TenantID == null || c.TenantID == tenantID)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<Crop?> CropGetByIdAsync(int idCrop)
        {
            var row = await db.Crops.AsNoTracking().FirstOrDefaultAsync(c => c.IDCrop == idCrop);
            return row == null ? null : ToDto(row);
        }

        public async Task<Crop> CropAddAsync(Crop crop)
        {
            var row = new CropRow
            {
                TenantID = crop.TenantID,
                Name = crop.Name,
                TypicalCycleDays = crop.TypicalCycleDays,
                QualityMetricsJson = crop.QualityMetricsJson,
            };
            db.Crops.Add(row);
            await db.SaveChangesAsync();
            return ToDto(row);
        }

        public async Task CropUpdateAsync(Crop crop)
        {
            var row = await db.Crops.FirstOrDefaultAsync(c => c.IDCrop == crop.IDCrop);
            if (row == null)
            {
                return;
            }
            row.Name = crop.Name;
            row.TypicalCycleDays = crop.TypicalCycleDays;
            row.QualityMetricsJson = crop.QualityMetricsJson;
            await db.SaveChangesAsync();
        }

        public async Task CropDeleteAsync(int idCrop) =>
            await db.Crops.Where(c => c.IDCrop == idCrop).ExecuteDeleteAsync();

        public async Task<int> CropFindOrCreateByNameAsync(int? tenantID, string name)
        {
            int? existing = await db.Crops.AsNoTracking()
                .Where(c => c.Name == name && (c.TenantID == null || c.TenantID == tenantID))
                .OrderByDescending(c => c.TenantID) // prefer the tenant's own row over an identically-named global one
                .Select(c => (int?)c.IDCrop)
                .FirstOrDefaultAsync();
            if (existing is int id)
            {
                return id;
            }
            var row = new CropRow { TenantID = tenantID, Name = name };
            db.Crops.Add(row);
            await db.SaveChangesAsync();
            return row.IDCrop;
        }

        private static Crop ToDto(CropRow c) => new()
        {
            IDCrop = c.IDCrop,
            TenantID = c.TenantID,
            Name = c.Name,
            TypicalCycleDays = c.TypicalCycleDays,
            QualityMetricsJson = c.QualityMetricsJson,
        };
    }
}
