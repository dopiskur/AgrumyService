using Agrumy.Shared.Models;
using Agrumy.Web.ViewModels;

namespace Agrumy.Api.Tests;

/// Fleet page Signal/Uptime columns show a dash for an offline device instead of a stale value, and a disabled device's badge stays white apart from a state-colored left stripe.
public class FleetRowDisplayTests
{
    [Fact]
    public void IsOffline_RealDeviceNotOnline_True() =>
        Assert.True(FleetRowDisplay.IsOffline(new DeviceFleetStatus { IsVirtual = false, Online = false }));

    [Fact]
    public void IsOffline_RealDeviceOnline_False() =>
        Assert.False(FleetRowDisplay.IsOffline(new DeviceFleetStatus { IsVirtual = false, Online = true }));

    [Fact]
    public void IsOffline_VirtualDeviceNeverConsideredOffline_EvenWhenOnlineIsFalse() =>
        Assert.False(FleetRowDisplay.IsOffline(new DeviceFleetStatus { IsVirtual = true, Online = false }));

    [Fact]
    public void DisabledStripeColor_Virtual_IsBlue() =>
        Assert.Equal("var(--bs-primary)", FleetRowDisplay.DisabledStripeColor(new DeviceFleetStatus { IsVirtual = true, Online = false }));

    [Fact]
    public void DisabledStripeColor_RealOnline_IsLightGreen() =>
        Assert.Equal("var(--bs-success-subtle)", FleetRowDisplay.DisabledStripeColor(new DeviceFleetStatus { IsVirtual = false, Online = true }));

    [Fact]
    public void DisabledStripeColor_RealOffline_IsLightRed() =>
        Assert.Equal("var(--bs-danger-subtle)", FleetRowDisplay.DisabledStripeColor(new DeviceFleetStatus { IsVirtual = false, Online = false }));
}
