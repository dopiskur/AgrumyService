using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Global (Global-admin maintained, TenantID null) + organization-own crop catalog (Detaljni dizajn R, D12) - an organization sees the union of both but can only edit its own rows.
    public interface ICropCatalogRepository
    {
        /// Global rows plus the given organization's own - the union D12 requires, never global-only or organization-only.
        Task<IList<Crop>> CropsGetAsync(int? tenantID);

        Task<Crop?> CropGetByIdAsync(int idCrop);

        /// tenantID null creates a global (Global-admin only, enforced by the caller's role check) catalog row.
        Task<Crop> CropAddAsync(Crop crop);

        Task CropUpdateAsync(Crop crop);

        Task CropDeleteAsync(int idCrop);

        /// CropAdd's own "find-or-create by name" helper for the interim (pre-R2-wizard) Sowing forms - looks up an existing organization-or-global row by name before inserting a new one, so re-adding the same crop name doesn't fork the catalog.
        Task<int> CropFindOrCreateByNameAsync(int? tenantID, string name);
    }
}
