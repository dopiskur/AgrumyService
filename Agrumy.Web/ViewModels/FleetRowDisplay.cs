using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Pure presentation rules for one _FleetRows.cshtml row, pulled out of the view so they're unit-testable without rendering Razor.
    public static class FleetRowDisplay
    {
        // Virtual devices have no real poll cycle so are never "offline" for this purpose - see DeviceFleetStatus.IsVirtual remarks.
        public static bool IsOffline(DeviceFleetStatus item) => !item.IsVirtual && !item.Online;

        // Disabled devices keep a white badge with only a left stripe carrying the underlying state's color.
        public static string DisabledStripeColor(DeviceFleetStatus item) => item.IsVirtual
            ? "var(--bs-primary)"
            : item.Online ? "var(--bs-success-subtle)" : "var(--bs-danger-subtle)";
    }
}
