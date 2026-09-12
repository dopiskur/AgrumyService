using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
using Agrumy.Shared.Models;
using Moq;

namespace Agrumy.Api.Tests;

/// ContractTests only proves a hand-built DeviceConfig serializes to a schema-valid
/// shape; it never exercises the real DeviceConfigBuilder.BuildAsync, so a field BuildAsync silently
/// forgets to map from Device (the user's own example: SleepSeconds never reaching firmware, caught
/// during the field rename) would sail through those tests unnoticed. This drives the real builder against a
/// fully-populated Device and asserts each top-level field actually carried over.
public class DeviceConfigBuilderTests
{
    // IServerConfigRepository is the primary mocked type; every other facet DeviceConfigBuilder/FirmwareCatalogService need is the same underlying mock viewed through Mock.As<T>() - none of these tests enable DeviceControllerEnabled, so the rule-hierarchy facets are never actually called.
    private readonly Mock<IServerConfigRepository> _repo = new(MockBehavior.Strict);
    private ITenantRepository TenantRepo => _repo.As<ITenantRepository>().Object;
    private IDeviceRepository DeviceRepo => _repo.As<IDeviceRepository>().Object;
    private ISimulationRepository SimulationRepo => _repo.As<ISimulationRepository>().Object;
    private IDeviceFarmUnitRepository FarmUnitRepo => _repo.As<IDeviceFarmUnitRepository>().Object;
    private IFarmParcelRepository FarmParcelRepo => _repo.As<IFarmParcelRepository>().Object;
    private ISowingRepository SowingRepo => _repo.As<ISowingRepository>().Object;
    private IExperimentRepository ExperimentRepo => _repo.As<IExperimentRepository>().Object;
    private IFirmwareRepository FirmwareRepo => _repo.As<IFirmwareRepository>().Object;
    private IDeviceOutboxRepository OutboxRepo => _repo.As<IDeviceOutboxRepository>().Object;

    // All facets must be registered via As&lt;T&gt;() before ANY .Object access on this mock - Moq locks the interface set on the first .Object read, and the properties above each read .Object as part of registering theirs.
    public DeviceConfigBuilderTests()
    {
        _repo.As<ITenantRepository>();
        _repo.As<IDeviceRepository>();
        _repo.As<ISimulationRepository>();
        _repo.As<IDeviceFarmUnitRepository>();
        _repo.As<IFarmParcelRepository>();
        _repo.As<ISowingRepository>();
        _repo.As<IExperimentRepository>();
        _repo.As<IFirmwareRepository>();
        _repo.As<IDeviceOutboxRepository>();
    }

    private DeviceConfigBuilder NewBuilder() => new(_repo.Object, TenantRepo, DeviceRepo, SimulationRepo, FarmUnitRepo, FarmParcelRepo, SowingRepo, ExperimentRepo,
        FirmwareTestSupport.NewCatalog(FirmwareRepo, _repo.Object, DeviceRepo),
        new DeviceOutboxService(OutboxRepo, DeviceRepo, FarmUnitRepo, FarmParcelRepo, new NoOpMqttCommandPublisher()));

    /// Every BuildAsync call now unconditionally consumes any pending ConfigChanged/HardReset outbox rows for the device - an empty pending list means ConsumeHardResetIfPendingAsync's own GetPendingOutboxItemsAsync read is the only outbox call, no HardReset means config.Reset comes back false.
    private void SetUpNoPendingOutboxItems(int deviceId)
    {
        _repo.As<IDeviceOutboxRepository>().Setup(r => r.GetPendingOutboxItemsAsync(deviceId)).ReturnsAsync(new List<DeviceCommand>());
        _repo.As<IDeviceOutboxRepository>().Setup(r => r.ConsumePendingByTypeAsync(deviceId, CommandActionType.ConfigChanged, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
    }

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
            FirmwareUpdate = false, // keeps ResolveOfferAsync a no-DB-call short circuit - firmware-offer mapping is covered by FirmwareCatalogServiceTests, not this test's concern
            Enabled = true,
        };

        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.As<ITenantRepository>().Setup(r => r.TenantGetByIdAsync(7)).ReturnsAsync(new Tenant { IDTenant = 7, EmergencyStopActive = false });
        _repo.As<IDeviceRepository>().Setup(r => r.DeviceSimulationGetAsync(1000038)).ReturnsAsync((DeviceSimulation?)null);
        SetUpNoPendingOutboxItems(1000038);

        var builder = NewBuilder();

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
        Assert.False(config.Reset); // no pending HardReset outbox item
        Assert.Equal(device.FirmwareUpdate, config.FirmwareUpdate);
        Assert.Equal(device.Enabled, config.Enabled);
        // Computed, not copied straight off Device - still required to actually be present, not left at their type default.
        Assert.NotNull(config.UtcOffsetSeconds);
        Assert.NotNull(config.ServerUtcEpoch);
        // BuildAsync never sets this explicitly - it relies on DeviceConfig.SchemaVersion's own property-initializer default, so this guards against that default ever silently getting lost.
        Assert.NotNull(config.SchemaVersion);
    }

    [Fact]
    public async Task BuildAsync_TenantEmergencyStopActive_CarriesOverToConfig()
    {
        var device = new Device { IDDevice = 5, TenantID = 9, FirmwareUpdate = false };
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.As<ITenantRepository>().Setup(r => r.TenantGetByIdAsync(9)).ReturnsAsync(new Tenant { IDTenant = 9, EmergencyStopActive = true });
        _repo.As<IDeviceRepository>().Setup(r => r.DeviceSimulationGetAsync(5)).ReturnsAsync((DeviceSimulation?)null);
        SetUpNoPendingOutboxItems(5);

        var builder = NewBuilder();
        DeviceConfig config = await builder.BuildAsync(device, pendingCommand: null, board: null);

        Assert.True(config.EmergencyStop);
    }

    [Fact]
    public async Task BuildAsync_PendingCommand_CarriesOverToConfig()
    {
        var device = new Device { IDDevice = 5, TenantID = 9, FirmwareUpdate = false };
        var pending = new PendingCommand { IDDeviceCommand = 11, ActionType = CommandActionType.Reboot, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.As<ITenantRepository>().Setup(r => r.TenantGetByIdAsync(9)).ReturnsAsync(new Tenant { IDTenant = 9 });
        _repo.As<IDeviceRepository>().Setup(r => r.DeviceSimulationGetAsync(5)).ReturnsAsync((DeviceSimulation?)null);
        SetUpNoPendingOutboxItems(5);

        var builder = NewBuilder();
        DeviceConfig config = await builder.BuildAsync(device, pending, board: null);

        Assert.Same(pending, config.PendingCommand);
    }
}
