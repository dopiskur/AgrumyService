using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/Farms.cshtml - Units is the dashboard-shaped list (not the plain CRUD model) so the same data feeds both the assign-unit picker and the Unit/Zone cube browser that moved here from the old Dashboard.
    public class FarmListViewModel
    {
        public IList<DeviceFarm> Farms { get; init; } = [];
        public IList<DeviceFarmUnitDashboard> Units { get; init; } = [];
        public IList<SowingDashboard> Crops { get; init; } = [];
        public IList<FarmOpenfield> Openfields { get; init; } = [];
    }
}
