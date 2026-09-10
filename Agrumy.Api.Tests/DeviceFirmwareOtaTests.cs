using Agrumy.Shared;
using Agrumy.Api.Commands;
using Agrumy.Api.Controllers.API;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Agrumy.Api.Tests.TestSupport;

namespace Agrumy.Api.Tests;

/// Exercises the firmware-lookup branch in DeviceApiController.BuildDeviceConfigAsync() through the public Register action (Config takes the same path). No database - IRepository is mocked.
public class DeviceFirmwareOtaTests
{
    private readonly Mock<IAllFacetsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();

    private DeviceConfig RegisterAndGetConfig(Device device, IList<DeviceCommand>? pendingOutboxItems = null)
    {
        _repo.Setup(r => r.UserGetAsync(null, "owner@example.com", null))
             .ReturnsAsync(new User { IDUser = 77, TenantID = device.TenantID, DevicePin = "ABC234", DevicePinExpires = DateTime.UtcNow.AddHours(1) });
        _repo.Setup(r => r.DeviceGetAsync(device.TenantID, null, null, "AABBCCDDEEFF"))
             .ReturnsAsync(device); // IDDevice set => controller skips DeviceAddAsync
        // BuildDeviceConfigAsync always reads ServerConfig and the device's own tenant (UtcOffsetSeconds); no ScheduleTimeZone configured, so the response's offset is 0/UTC.
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.Setup(r => r.TenantGetByIdAsync(device.TenantID!.Value)).ReturnsAsync(new Tenant { IDTenant = device.TenantID });
        // DeviceRegistration always checks for a pending command before returning; BuildAsync also unconditionally consumes any pending ConfigChanged row.
        _repo.Setup(r => r.GetPendingOutboxItemsAsync(device.IDDevice!.Value)).ReturnsAsync(pendingOutboxItems ?? new List<DeviceCommand>());
        _repo.Setup(r => r.ConsumePendingByTypeAsync(device.IDDevice!.Value, CommandActionType.ConfigChanged, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeviceSimulationGetAsync(device.IDDevice!.Value)).ReturnsAsync((DeviceSimulation?)null);

        var catalog = FirmwareTestSupport.NewCatalog(_repo.Object, _repo.Object, _repo.Object);
        var outboxService = new DeviceOutboxService(_repo.Object, _repo.Object, _repo.Object, new NoOpMqttCommandPublisher());
        var controller = new DeviceApiController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object,
            outboxService, catalog,
            new Agrumy.Api.Devices.DeviceConfigBuilder(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, catalog, outboxService),
            Microsoft.Extensions.Options.Options.Create(new AgrumySettings()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceApiController>.Instance,
            new Agrumy.Api.Quota.TenantQuotaEnforcer(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object));
        var result = controller.DeviceRegistration(new DeviceRegistration
        {
            Email = "owner@example.com",
            DevicePin = "ABC234",
            MacAddress = "AABBCCDDEEFF",
        }).GetAwaiter().GetResult();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<DeviceConfig>(ok.Value);
    }

    private static Device BaseDevice() => new()
    {
        IDDevice = 500,
        TenantID = 0,
        DeviceRoleID = 7,
        ApiId = "api-id",
        ApiKey = "api-key",
        DeviceSensorEnabled = false,
        DeviceControllerEnabled = false,
    };

    [Fact]
    public void FirmwareUpdateFlagSet_PopulatesVersionAndUrlFromLatestBuild()
    {
        var device = BaseDevice();
        device.FirmwareUpdate = true;

        _repo.Setup(r => r.DeviceFirmwareLatestGetAsync(7))
             .ReturnsAsync(new DeviceFirmware
             {
                 DeviceTypeID = 7,
                 Version = "0.2.0",
                 Url = "https://cdn.agrumy.com/fw/esp32/0.2.0.bin",
             });

        var cfg = RegisterAndGetConfig(device);

        Assert.True(cfg.FirmwareUpdate);
        Assert.Equal("0.2.0", cfg.FirmwareVersion);
        Assert.Equal("https://cdn.agrumy.com/fw/esp32/0.2.0.bin", cfg.FirmwareUrl);
    }

    [Fact]
    public void FirmwareUpdateFlagClear_DoesNotQueryFirmware_AndLeavesFieldsNull()
    {
        var device = BaseDevice();
        device.FirmwareUpdate = false;

        var cfg = RegisterAndGetConfig(device);

        Assert.Null(cfg.FirmwareVersion);
        Assert.Null(cfg.FirmwareUrl);
        // Strict mock: a call to DeviceFirmwareLatestGetAsync would have thrown.

        _repo.Verify(r => r.DeviceFirmwareLatestGetAsync(It.IsAny<int?>()), Times.Never);
    }

    /// #357: Reset rides along on this same Register/Config response, but - unlike FirmwareUpdate, which waits for the device to confirm the new version before clearing - it clears the instant it's included, since a device that resets() wipes itself before it could ever confirm anything.
    [Fact]
    public void ResetFlagSet_ClearsImmediately_NotOnConfirmation()
    {
        var device = BaseDevice();
        _repo.Setup(r => r.ConsumePendingByTypeAsync(500, CommandActionType.HardReset, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        var pendingHardReset = new List<DeviceCommand>
        {
            new() { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.HardReset, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddDays(365) },
        };

        var cfg = RegisterAndGetConfig(device, pendingHardReset);

        Assert.True(cfg.Reset);
        _repo.Verify(r => r.ConsumePendingByTypeAsync(500, CommandActionType.HardReset, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public void ResetFlagClear_NeverCallsHardResetSet()
    {
        var device = BaseDevice();

        var cfg = RegisterAndGetConfig(device);

        Assert.False(cfg.Reset);
        // Strict mock: ConsumePendingByTypeAsync(..., HardReset, ...) was never set up - no pending row must not attempt a clear-write.
    }

    [Fact]
    public void FirmwareUpdateFlagSet_ButNoBuildRow_LeavesFieldsNull_NoThrow()
    {
        var device = BaseDevice();
        device.FirmwareUpdate = true;
        _repo.Setup(r => r.DeviceFirmwareLatestGetAsync(7)).ReturnsAsync((DeviceFirmware?)null);

        var cfg = RegisterAndGetConfig(device);

        Assert.True(cfg.FirmwareUpdate);
        Assert.Null(cfg.FirmwareVersion);
        Assert.Null(cfg.FirmwareUrl);
    }

    [Fact]
    public void FirmwareUpdateFlagSet_ButDeviceTypeNull_DoesNotQueryFirmware()
    {
        var device = BaseDevice();
        device.FirmwareUpdate = true;
        device.DeviceRoleID = null;

        var cfg = RegisterAndGetConfig(device);

        Assert.Null(cfg.FirmwareVersion);
        _repo.Verify(r => r.DeviceFirmwareLatestGetAsync(It.IsAny<int?>()), Times.Never);
    }
}
