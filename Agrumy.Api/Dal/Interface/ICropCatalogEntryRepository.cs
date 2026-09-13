using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// CRUD over the six crop catalog tables (Arable/Fruit/Vegetable/Industrial/Ornamental/MedicinalAndAromatic), all with the identical CropCatalogEntry shape - the type parameter picks which table, never a mixed/joined query across them. Distinct from ICropCatalogRepository, which is the unrelated simple global/tenant Crop-name catalog used by Sowing (D12).
    public interface ICropCatalogEntryRepository
    {
        Task<IList<CropCatalogEntry>> CatalogGetAsync(CropCatalogType type);
        Task<CropCatalogEntry?> CatalogGetByIdAsync(CropCatalogType type, int id);
        Task<CropCatalogEntry> CatalogAddAsync(CropCatalogType type, CropCatalogEntry entry);
        Task<bool> CatalogUpdateAsync(CropCatalogType type, CropCatalogEntry entry);
        Task<bool> CatalogDeleteAsync(CropCatalogType type, int id);
    }
}
