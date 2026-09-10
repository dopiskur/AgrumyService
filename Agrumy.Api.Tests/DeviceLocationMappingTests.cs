using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests;

/// DeviceMappingExtensions' location precedence rules, pure C# with no DB involved - the GPS-overwrites-manual side lives in EfDeviceRepository and is covered by RelationalIntegrationTests instead.
public class DeviceLocationMappingTests
{
    [Fact]
    public void ApplyTo_BothCoordinatesGiven_SetsManualSource()
    {
        var target = new DeviceDto { LocationSource = DeviceLocationSource.Gps, Latitude = 1, Longitude = 2 };
        var form = new DeviceEditForm { Latitude = 45.8, Longitude = 15.9 };

        form.ApplyTo(target);

        Assert.Equal(45.8, target.Latitude);
        Assert.Equal(15.9, target.Longitude);
        Assert.Equal(DeviceLocationSource.Manual, target.LocationSource);
    }

    [Fact]
    public void ApplyTo_BothCoordinatesNull_LeavesExistingLocationUntouched()
    {
        var target = new DeviceDto { LocationSource = DeviceLocationSource.Gps, Latitude = 1, Longitude = 2 };
        var form = new DeviceEditForm();

        form.ApplyTo(target);

        Assert.Equal(1, target.Latitude);
        Assert.Equal(2, target.Longitude);
        Assert.Equal(DeviceLocationSource.Gps, target.LocationSource);
    }

    [Fact]
    public void ToEditForm_SourceIsGps_BlanksCoordinatesSoAnUntouchedResubmitCannotClobberTheFix()
    {
        var dto = new DeviceDto { LocationSource = DeviceLocationSource.Gps, Latitude = 1, Longitude = 2 };

        var form = dto.ToEditForm();

        Assert.Null(form.Latitude);
        Assert.Null(form.Longitude);
    }

    [Fact]
    public void ToEditForm_SourceIsManual_PrefillsCurrentCoordinates()
    {
        var dto = new DeviceDto { LocationSource = DeviceLocationSource.Manual, Latitude = 45.8, Longitude = 15.9 };

        var form = dto.ToEditForm();

        Assert.Equal(45.8, form.Latitude);
        Assert.Equal(15.9, form.Longitude);
    }
}
