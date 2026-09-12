using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/RecycleBin.cshtml.
    public class RecycleBinViewModel
    {
        public IList<DeviceDto> Devices { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
        // Marked for permanent removal but still restorable until the purge cycle actually reaps them.
        public IList<DeviceDto> PendingPurgeDevices { get; init; } = [];
        public IList<DeviceFarm> PendingPurgeFarms { get; init; } = [];
        public int RecycleBinRetentionDays { get; init; }
    }
}
