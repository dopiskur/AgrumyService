using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IFirmwareRepository members - forwarded to the standalone EfFirmwareRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<IList<DeviceFirmware>> FirmwareListAsync() => firmwareRepository.FirmwareListAsync();

        public Task<DeviceFirmware?> FirmwareGetAsync(int idDeviceFirmware) => firmwareRepository.FirmwareGetAsync(idDeviceFirmware);

        public Task<IList<DeviceFirmware>> FirmwareListForBoardAsync(string board, IReadOnlyCollection<FirmwareSource> sources) =>
            firmwareRepository.FirmwareListForBoardAsync(board, sources);

        public Task<int> FirmwareAddAsync(DeviceFirmware firmware) => firmwareRepository.FirmwareAddAsync(firmware);

        public Task FirmwareDeleteAsync(int idDeviceFirmware) => firmwareRepository.FirmwareDeleteAsync(idDeviceFirmware);

        public Task<int> FirmwareDeleteBySourceAsync(FirmwareSource source) => firmwareRepository.FirmwareDeleteBySourceAsync(source);

        public Task<int> FirmwareReplaceSourceRowsAsync(FirmwareSource source, IReadOnlyList<DeviceFirmware> rows) =>
            firmwareRepository.FirmwareReplaceSourceRowsAsync(source, rows);

        public Task DeviceFirmwareUpdateSetAsync(int idDevice, bool update, string? targetVersion) =>
            firmwareRepository.DeviceFirmwareUpdateSetAsync(idDevice, update, targetVersion);

        public Task<string?> DeviceBoardGetAsync(int idDevice) => firmwareRepository.DeviceBoardGetAsync(idDevice);
    }
}
