using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Add/Edit form model - HorticultureCatalogEntry itself carries no CatalogType, since that's which TABLE it lives in (the API route param), not a field on the row.
    public class HorticultureCatalogEditViewModel
    {
        public HorticultureCatalogType CatalogType { get; set; }
        public HorticultureCatalogEntry Entry { get; set; } = new();
    }
}
