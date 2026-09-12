using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/_FarmsAndUnits.cshtml - Greenhouse farms/units only (D1), Open-Field lives on its own page.
    public class GroupedUnitCubesViewModel
    {
        public IList<DeviceFarmUnitDashboard> Units { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
    }
}
