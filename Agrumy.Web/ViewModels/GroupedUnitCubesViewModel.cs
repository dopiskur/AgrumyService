using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/_FarmsAndUnits.cshtml.
    public class GroupedUnitCubesViewModel
    {
        public IList<DeviceFarmUnitDashboard> Units { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
    }
}
