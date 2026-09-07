using api.Models;

namespace api.ViewModels
{
    /// Drives DeviceFarmUnit/RecycleBin.cshtml (roadmap #409).
    public class RecycleBinViewModel
    {
        public IList<DeviceDto> Devices { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
        public int RecycleBinRetentionDays { get; init; }
    }
}
