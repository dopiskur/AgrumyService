using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/RecycleBin.cshtml (roadmap #409/#427).
    public class RecycleBinViewModel
    {
        public IList<DeviceDto> Devices { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
        // Roadmap #427 - marked for permanent removal but still restorable until the purge cycle actually reaps them.
        public IList<DeviceDto> PendingPurgeDevices { get; init; } = [];
        public IList<DeviceFarm> PendingPurgeFarms { get; init; } = [];
        public int RecycleBinRetentionDays { get; init; }
    }
}
