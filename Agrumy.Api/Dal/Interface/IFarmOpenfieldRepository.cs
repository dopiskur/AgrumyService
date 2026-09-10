using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Open-Field facet of the data layer - Crop/Parcel CRUD, device assignment, and per-widget dashboard aggregation. Mirrors IDeviceFarmUnitRepository's Unit/Zone facet field-for-field; split into its own interface (rather than folded into IDeviceFarmUnitRepository) since it's a fully parallel, independently-growing domain.
    public interface IFarmOpenfieldRepository
    {
        // ---- Farm type-extension row -------------------------------------

        /// Creates the Farm (FarmType=OpenField) and its FarmOpenfield extension row together, in one call - quotaCheckAsync (when given) is the same MaxFarms check DeviceFarmAddAsync uses, shared across both branches.
        Task<(DeviceFarm Farm, FarmOpenfield Openfield)> FarmOpenfieldCreateAsync(string? farmName, int? tenantID, Func<Task<string?>>? quotaCheckAsync = null);

        Task<FarmOpenfield?> FarmOpenfieldGetByFarmIdAsync(int idFarm);

        /// Every Open-Field extension row in scope - the Web layer's way to map a Farm to its FarmOpenfieldID in bulk (one call, not one FarmOpenfieldGetByFarmIdAsync per farm) when grouping crops onto the Farms page.
        Task<IList<FarmOpenfield>> FarmOpenfieldsGetAsync(int? tenantID);

        // ---- Crop CRUD ----------------------------------------------------

        Task<IList<FarmOpenfieldCrop>> CropsGetAsync(int? tenantID);

        Task<FarmOpenfieldCrop?> CropGetByIdAsync(int? idFarmOpenfieldCrop);

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert - see Agrumy.Api.Quota.QuotaGuard.
        Task<FarmOpenfieldCrop> CropAddAsync(FarmOpenfieldCrop crop, Func<Task<string?>>? quotaCheckAsync = null);

        Task CropUpdateAsync(FarmOpenfieldCrop crop);

        Task CropsReorderAsync(int tenantId, IReadOnlyList<int> orderedCropIds);

        Task CropDeleteAsync(int idFarmOpenfieldCrop);

        // ---- Parcel CRUD ----------------------------------------------------

        Task<IList<FarmOpenfieldCropParcel>> ParcelsGetAsync(int idFarmOpenfieldCrop);

        Task<FarmOpenfieldCropParcel?> ParcelGetByIdAsync(int? idFarmOpenfieldCropParcel);

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert - see Agrumy.Api.Quota.QuotaGuard.
        Task<FarmOpenfieldCropParcel> ParcelAddAsync(FarmOpenfieldCropParcel parcel, Func<Task<string?>>? quotaCheckAsync = null);

        Task ParcelUpdateAsync(FarmOpenfieldCropParcel parcel);

        /// Moves a parcel to a different crop - mirrors DeviceFarmUnitZoneMigrateAsync (also updates the denormalized device FK and bumps ConfigVersion). Returns false if the parcel doesn't exist.
        Task<bool> ParcelMigrateAsync(int idFarmOpenfieldCropParcel, int idTargetFarmOpenfieldCrop);

        /// Bumps ConfigVersion for every device in the parcel - mirrors DeviceFarmUnitZoneConfigVersionBumpAsync.
        Task ParcelConfigVersionBumpAsync(int idFarmOpenfieldCropParcel);

        Task ParcelDeleteAsync(int idFarmOpenfieldCropParcel);

        // ---- Device assignment -----------------------------------------

        Task DeviceAssignToParcelAsync(int idDevice, int idFarmOpenfieldCropParcel);

        Task DeviceUnassignFromParcelAsync(int idDevice);

        /// Open-Field's equivalent of IDeviceFarmUnitRepository.DeviceFarmUnitZoneHasControllerAsync - a parcel has at most one controller, same cap as a zone.
        Task<bool> ParcelHasControllerAsync(int idFarmOpenfieldCropParcel);

        // ---- Dashboard widget aggregation -----------------

        /// One widget's own (level, levelId) scope, Crop/Parcel arms of IDeviceFarmUnitRepository.DashboardAggregateGetAsync's switch.
        Task<(SensorAverages Averages, SensorTrend Trend)> CropAggregateAsync(int idFarmOpenfieldCrop);

        Task<(SensorAverages Averages, SensorTrend Trend)> ParcelAggregateAsync(int idFarmOpenfieldCropParcel);
    }
}
