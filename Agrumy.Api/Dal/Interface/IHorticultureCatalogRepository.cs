using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// CRUD over the three horticulture catalog tables (Crop/Perma/Hydroponic), all with the identical HorticultureCatalogEntry shape - the type parameter picks which table, never a mixed/joined query across them.
    public interface IHorticultureCatalogRepository
    {
        Task<IList<HorticultureCatalogEntry>> CatalogGetAsync(HorticultureCatalogType type);
        Task<HorticultureCatalogEntry?> CatalogGetByIdAsync(HorticultureCatalogType type, int id);
        Task<HorticultureCatalogEntry> CatalogAddAsync(HorticultureCatalogType type, HorticultureCatalogEntry entry);
        Task<bool> CatalogUpdateAsync(HorticultureCatalogType type, HorticultureCatalogEntry entry);
        Task<bool> CatalogDeleteAsync(HorticultureCatalogType type, int id);
    }
}
