using api.Models;

namespace api.ViewModels
{
    /// Drives DeviceFarmUnit/_UnitCubesGrouped.cshtml.
    public class GroupedUnitCubesViewModel
    {
        public IList<DeviceFarmUnitDashboard> Units { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
    }
}
