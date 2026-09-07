using api.Models;

namespace api.ViewModels
{
    /// Drives DeviceFarmUnit/_UnitCubesGrouped.cshtml (roadmap #412 (4)).
    public class GroupedUnitCubesViewModel
    {
        public IList<DeviceFarmUnitDashboard> Units { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
    }
}
