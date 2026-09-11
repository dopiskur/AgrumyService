using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// The FarmOpenfield 1:1 type-extension row only - Crop/Parcel CRUD moved to ISowingRepository/IFarmParcelRepository/ICropCatalogRepository (restructure R).
    public interface IFarmOpenfieldRepository
    {
        /// Creates the Farm (FarmType=OpenField) and its FarmOpenfield extension row together, in one call - quotaCheckAsync (when given) is the same MaxFarms check DeviceFarmAddAsync uses, shared across both branches.
        Task<(DeviceFarm Farm, FarmOpenfield Openfield)> FarmOpenfieldCreateAsync(string? farmName, int? tenantID, Func<Task<string?>>? quotaCheckAsync = null);

        Task<FarmOpenfield?> FarmOpenfieldGetByFarmIdAsync(int idFarm);

        /// Every Open-Field extension row in scope - the Web layer's way to map a Farm to its FarmOpenfieldID in bulk (one call, not one FarmOpenfieldGetByFarmIdAsync per farm) when grouping crops onto the Farms page.
        Task<IList<FarmOpenfield>> FarmOpenfieldsGetAsync(int? tenantID);
    }
}
