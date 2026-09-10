using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IFarmOpenfieldRepository members - forwarded to the standalone EfFarmOpenfieldRepository so IAllFacetsRepository's broad consumers keep working unchanged.
    internal sealed partial class AllFacetsRepository
    {
        public Task<(DeviceFarm Farm, FarmOpenfield Openfield)> FarmOpenfieldCreateAsync(string? farmName, int? tenantID, Func<Task<string?>>? quotaCheckAsync = null) =>
            farmOpenfieldRepository.FarmOpenfieldCreateAsync(farmName, tenantID, quotaCheckAsync);

        public Task<FarmOpenfield?> FarmOpenfieldGetByFarmIdAsync(int idFarm) => farmOpenfieldRepository.FarmOpenfieldGetByFarmIdAsync(idFarm);

        public Task<IList<FarmOpenfield>> FarmOpenfieldsGetAsync(int? tenantID) => farmOpenfieldRepository.FarmOpenfieldsGetAsync(tenantID);

        public Task<IList<FarmOpenfieldCrop>> CropsGetAsync(int? tenantID) => farmOpenfieldRepository.CropsGetAsync(tenantID);

        public Task<FarmOpenfieldCrop?> CropGetByIdAsync(int? idFarmOpenfieldCrop) => farmOpenfieldRepository.CropGetByIdAsync(idFarmOpenfieldCrop);

        public Task<FarmOpenfieldCrop> CropAddAsync(FarmOpenfieldCrop crop, Func<Task<string?>>? quotaCheckAsync = null) => farmOpenfieldRepository.CropAddAsync(crop, quotaCheckAsync);

        public Task CropUpdateAsync(FarmOpenfieldCrop crop) => farmOpenfieldRepository.CropUpdateAsync(crop);

        public Task CropsReorderAsync(int tenantId, IReadOnlyList<int> orderedCropIds) => farmOpenfieldRepository.CropsReorderAsync(tenantId, orderedCropIds);

        public Task CropDeleteAsync(int idFarmOpenfieldCrop) => farmOpenfieldRepository.CropDeleteAsync(idFarmOpenfieldCrop);

        public Task<IList<FarmOpenfieldCropParcel>> ParcelsGetAsync(int idFarmOpenfieldCrop) => farmOpenfieldRepository.ParcelsGetAsync(idFarmOpenfieldCrop);

        public Task<FarmOpenfieldCropParcel?> ParcelGetByIdAsync(int? idFarmOpenfieldCropParcel) => farmOpenfieldRepository.ParcelGetByIdAsync(idFarmOpenfieldCropParcel);

        public Task<FarmOpenfieldCropParcel> ParcelAddAsync(FarmOpenfieldCropParcel parcel, Func<Task<string?>>? quotaCheckAsync = null) => farmOpenfieldRepository.ParcelAddAsync(parcel, quotaCheckAsync);

        public Task ParcelUpdateAsync(FarmOpenfieldCropParcel parcel) => farmOpenfieldRepository.ParcelUpdateAsync(parcel);

        public Task<bool> ParcelMigrateAsync(int idFarmOpenfieldCropParcel, int idTargetFarmOpenfieldCrop) => farmOpenfieldRepository.ParcelMigrateAsync(idFarmOpenfieldCropParcel, idTargetFarmOpenfieldCrop);

        public Task ParcelConfigVersionBumpAsync(int idFarmOpenfieldCropParcel) => farmOpenfieldRepository.ParcelConfigVersionBumpAsync(idFarmOpenfieldCropParcel);

        public Task ParcelDeleteAsync(int idFarmOpenfieldCropParcel) => farmOpenfieldRepository.ParcelDeleteAsync(idFarmOpenfieldCropParcel);

        public Task DeviceAssignToParcelAsync(int idDevice, int idFarmOpenfieldCropParcel) => farmOpenfieldRepository.DeviceAssignToParcelAsync(idDevice, idFarmOpenfieldCropParcel);

        public Task DeviceUnassignFromParcelAsync(int idDevice) => farmOpenfieldRepository.DeviceUnassignFromParcelAsync(idDevice);

        public Task<bool> ParcelHasControllerAsync(int idFarmOpenfieldCropParcel) => farmOpenfieldRepository.ParcelHasControllerAsync(idFarmOpenfieldCropParcel);

        public Task<(SensorAverages Averages, SensorTrend Trend)> CropAggregateAsync(int idFarmOpenfieldCrop) => farmOpenfieldRepository.CropAggregateAsync(idFarmOpenfieldCrop);

        public Task<(SensorAverages Averages, SensorTrend Trend)> ParcelAggregateAsync(int idFarmOpenfieldCropParcel) => farmOpenfieldRepository.ParcelAggregateAsync(idFarmOpenfieldCropParcel);

        public Task<IList<FarmOpenfieldCropDashboard>> CropDashboardGetAsync(int? tenantID) => farmOpenfieldRepository.CropDashboardGetAsync(tenantID);

        public Task<IList<FarmOpenfieldCropParcelDashboard>> ParcelDashboardListGetAsync(int idFarmOpenfieldCrop) => farmOpenfieldRepository.ParcelDashboardListGetAsync(idFarmOpenfieldCrop);
    }
}
