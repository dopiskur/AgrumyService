using Agrumy.Shared.Models;
using Agrumy.Web.ViewModels;

namespace Agrumy.Api.Tests;

/// Fleet page Signal/Uptime columns show a dash for an offline device instead of a stale value, and a disabled device's badge stays mostly white apart from a state-colored stripe.
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
    public void DisabledStripeColor_RealOnline_IsGreen() =>
        Assert.Equal("var(--bs-success)", FleetRowDisplay.DisabledStripeColor(new DeviceFleetStatus { IsVirtual = false, Online = true }));

    [Fact]
    public void DisabledStripeColor_RealOffline_IsRed() =>
        Assert.Equal("var(--bs-danger)", FleetRowDisplay.DisabledStripeColor(new DeviceFleetStatus { IsVirtual = false, Online = false }));

    [Theory]
    [InlineData(100, "bg-battery-full")]
    [InlineData(85, "bg-battery-full")]
    [InlineData(84, "bg-battery-good")]
    [InlineData(60, "bg-battery-good")]
    [InlineData(59, "bg-battery-medium")]
    [InlineData(40, "bg-battery-medium")]
    [InlineData(39, "bg-battery-low")]
    [InlineData(25, "bg-battery-low")]
    [InlineData(24, "bg-battery-critical")]
    [InlineData(0, "bg-battery-critical")]
    public void BatteryBadgeCss_TierBoundaries(int battery, string expectedCss) =>
        Assert.Equal(expectedCss, FleetRowDisplay.BatteryBadgeCss(battery));
}
