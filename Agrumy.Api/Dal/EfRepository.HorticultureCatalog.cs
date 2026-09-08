using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal
{
    /// IHorticultureCatalogRepository members - forwarded to the standalone EfHorticultureCatalogRepository so IRepository's broad consumers keep working unchanged.
    internal partial class EfRepository
    {
        public Task<IList<HorticultureCatalogEntry>> CatalogGetAsync(HorticultureCatalogType type) => horticultureCatalogRepository.CatalogGetAsync(type);

        public Task<HorticultureCatalogEntry?> CatalogGetByIdAsync(HorticultureCatalogType type, int id) => horticultureCatalogRepository.CatalogGetByIdAsync(type, id);

        public Task<HorticultureCatalogEntry> CatalogAddAsync(HorticultureCatalogType type, HorticultureCatalogEntry entry) => horticultureCatalogRepository.CatalogAddAsync(type, entry);

        public Task<bool> CatalogUpdateAsync(HorticultureCatalogType type, HorticultureCatalogEntry entry) => horticultureCatalogRepository.CatalogUpdateAsync(type, entry);

        public Task<bool> CatalogDeleteAsync(HorticultureCatalogType type, int id) => horticultureCatalogRepository.CatalogDeleteAsync(type, id);
    }
}
