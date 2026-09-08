using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
using Agrumy.Shared.Models;
using Moq;

namespace Agrumy.Api.Tests;

/// Roadmap #397(5) - ContractTests only proves a hand-built DeviceConfig serializes to a schema-valid
/// shape; it never exercises the real DeviceConfigBuilder.BuildAsync, so a field BuildAsync silently
/// forgets to map from Device (the user's own example: SleepSeconds never reaching firmware, caught
/// during #385) would sail through those tests unnoticed. This drives the real builder against a
/// fully-populated Device and asserts each top-level field actually carried over.
public class DeviceConfigBuilderTests
{
    private readonly Mock<IRepository> _repo = new(MockBehavior.Strict);

    [Fact]
    public async Task BuildAsync_RealBuilder_PopulatesEveryTopLevelFieldFromDevice()
    {
        var device = new Device
        {
            ConfigVersion = 42,
            IDDevice = 1000038,
            TenantID = 7,
            DeviceFarmUnitID = 3,
            DeviceFarmUnitZoneID = 5,
            DeviceTypeServiceID = 2,
            ApiId = "api-id-123",
            ApiKey = "api-key-456",
            ServicePoint = "api.agrumy.com",
            ServicePublicKey = "pubkey-789",
            SleepSeconds = 120,
            SleepDeepEnabled = true,
            LoRaGatewayEnabled = true,
            DeviceSensorEnabled = false,
            DeviceControllerEnabled = false,
            BatteryEnabled = true,
            Debug = true,
            Reboot = false,
            Reset = false,
            FirmwareUpdate = false, // keeps ResolveOfferAsync a no-DB-call short circuit - firmware-offer mapping is covered by FirmwareCatalogServiceTests, not this test's concern
            Enabled = true,
        };

        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.Setup(r => r.TenantGetByIdAsync(7)).ReturnsAsync(new Tenant { IDTenant = 7, EmergencyStopActive = false });
        _repo.Setup(r => r.DeviceSimulationGetAsync(1000038)).ReturnsAsync((DeviceSimulation?)null);

        var builder = new DeviceConfigBuilder(_repo.Object, FirmwareTestSupport.NewCatalog(_repo.Object));

        DeviceConfig config = await builder.BuildAsync(device, pendingCommand: null, board: null);

        Assert.Equal(device.ConfigVersion, config.ConfigVersion);
        Assert.Equal(device.TenantID, config.TenantID);
        Assert.Equal(device.IDDevice, config.deviceID);
        Assert.Equal(device.DeviceFarmUnitID, config.DeviceFarmUnitID);
        Assert.Equal(device.DeviceFarmUnitZoneID, config.DeviceFarmUnitZoneID);
        Assert.Equal(device.DeviceTypeServiceID, config.DeviceTypeServiceID);
        Assert.Equal(device.ApiId, config.ApiId);
        Assert.Equal(device.ApiKey, config.ApiKey);
        Assert.Equal(device.ServicePoint, config.ServicePoint);
        Assert.Equal(device.ServicePublicKey, config.ServicePublicKey);
        Assert.Equal(device.SleepSeconds, config.SleepSeconds);
        Assert.Equal(device.SleepDeepEnabled, config.SleepDeep);
        Assert.Equal(device.LoRaGatewayEnabled, config.LoRaGatewayEnabled);
        Assert.Equal(device.DeviceSensorEnabled, config.DeviceSensorEnabled);
        Assert.Equal(device.DeviceControllerEnabled, config.DeviceControllerEnabled);
        Assert.Equal(device.BatteryEnabled, config.BatteryEnabled);
        Assert.Equal(device.Debug, config.Debug);
        Assert.Equal(device.Reboot, config.Reboot);
        Assert.Equal(device.Reset, config.Reset);
        Assert.Equal(device.FirmwareUpdate, config.FirmwareUpdate);
        Assert.Equal(device.Enabled, config.Enabled);
        // Computed, not copied straight off Device - still required to actually be present, not left at their type default.
        Assert.NotNull(config.UtcOffsetSeconds);
        Assert.NotNull(config.ServerUtcEpoch);
    }

    [Fact]
    public async Task BuildAsync_TenantEmergencyStopActive_CarriesOverToConfig()
    {
        var device = new Device { IDDevice = 5, TenantID = 9, FirmwareUpdate = false };
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.Setup(r => r.TenantGetByIdAsync(9)).ReturnsAsync(new Tenant { IDTenant = 9, EmergencyStopActive = true });
        _repo.Setup(r => r.DeviceSimulationGetAsync(5)).ReturnsAsync((DeviceSimulation?)null);

        var builder = new DeviceConfigBuilder(_repo.Object, FirmwareTestSupport.NewCatalog(_repo.Object));
        DeviceConfig config = await builder.BuildAsync(device, pendingCommand: null, board: null);

        Assert.True(config.EmergencyStop);
    }

    [Fact]
    public async Task BuildAsync_PendingCommand_CarriesOverToConfig()
    {
        var device = new Device { IDDevice = 5, TenantID = 9, FirmwareUpdate = false };
        var pending = new PendingCommand { IDDeviceCommand = 11, ActionType = CommandActionType.Reboot, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.Setup(r => r.TenantGetByIdAsync(9)).ReturnsAsync(new Tenant { IDTenant = 9 });
        _repo.Setup(r => r.DeviceSimulationGetAsync(5)).ReturnsAsync((DeviceSimulation?)null);

        var builder = new DeviceConfigBuilder(_repo.Object, FirmwareTestSupport.NewCatalog(_repo.Object));
        DeviceConfig config = await builder.BuildAsync(device, pending, board: null);

        Assert.Same(pending, config.PendingCommand);
    }
}
