using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Add form model, also reused read-only by Details.cshtml - CropCatalogEntry itself carries no CatalogType, since that's which TABLE it lives in (the API route param), not a field on the row.
    public class CropCatalogEditViewModel
    {
        public CropCatalogType CatalogType { get; set; }
        public CropCatalogEntry Entry { get; set; } = new();
    }
}
