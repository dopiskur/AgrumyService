using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/_DashboardWidgets.cshtml - Leaf is a Zone or (roadmap #519) a Parcel, both IFarmLeafLevelNode.
    public class DashboardWidgetsViewModel
    {
        public required IFarmLeafLevelNode Leaf { get; init; }
        /// Only set for the Zone case - IsParcelLeaf tells the view which WidgetAdd/Remove/Move action and hidden field name to post to.
        public bool IsParcelLeaf { get; init; }
        /// This zone's own dashboard - still used as the RelayStatus/target-picker default, no longer the sole data source every widget reads from.
        public required DeviceFarmUnitZoneDashboard Dashboard { get; init; }
        /// Whole-tenant fleet (not pre-filtered to Zone) - a RelayStatus widget can now target a different zone than the page it's shown on.
        public IList<DeviceFleetStatus> Fleet { get; init; } = [];
        public bool CanManage { get; init; }
        public IList<DeviceFarm> Farms { get; init; } = [];
        public IList<DeviceFarmUnit> Units { get; init; } = [];
        public IList<ZoneOption> Zones { get; init; } = [];
        /// One precomputed DashboardAggregate per distinct (level, levelId) target used by this zone's widgets.
        public IReadOnlyDictionary<(HierarchyNodeKind Level, int LevelId), DashboardAggregate> WidgetData { get; init; } =
            new Dictionary<(HierarchyNodeKind, int), DashboardAggregate>();
    }
}
