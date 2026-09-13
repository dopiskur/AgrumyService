using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// ICropCatalogEntryRepository members - forwarded to the standalone EfCropCatalogEntryRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<IList<CropCatalogEntry>> CatalogGetAsync(CropCatalogType type) => cropCatalogEntryRepository.CatalogGetAsync(type);

        public Task<CropCatalogEntry?> CatalogGetByIdAsync(CropCatalogType type, int id) => cropCatalogEntryRepository.CatalogGetByIdAsync(type, id);

        public Task<CropCatalogEntry> CatalogAddAsync(CropCatalogType type, CropCatalogEntry entry) => cropCatalogEntryRepository.CatalogAddAsync(type, entry);

        public Task<bool> CatalogUpdateAsync(CropCatalogType type, CropCatalogEntry entry) => cropCatalogEntryRepository.CatalogUpdateAsync(type, entry);

        public Task<bool> CatalogDeleteAsync(CropCatalogType type, int id) => cropCatalogEntryRepository.CatalogDeleteAsync(type, id);
    }
}
