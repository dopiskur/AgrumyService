using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/_DashboardWidgets.cshtml (roadmap #238).
    public class DashboardWidgetsViewModel
    {
        public required DeviceFarmUnitZone Zone { get; init; }
        public required DeviceFarmUnitZoneDashboard Dashboard { get; init; }
        public IList<DeviceFleetStatus> Fleet { get; init; } = [];
        public bool CanManage { get; init; }
    }
}
