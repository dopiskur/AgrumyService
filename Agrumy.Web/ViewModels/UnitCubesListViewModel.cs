using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/_UnitCubes.cshtml - Farms carried alongside Units so each cube can offer "Move to.../Unassign" without a second, separate unit list elsewhere on the page (roadmap #429).
    public class UnitCubesListViewModel
    {
        public IList<DeviceFarmUnitDashboard> Units { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
    }
}
