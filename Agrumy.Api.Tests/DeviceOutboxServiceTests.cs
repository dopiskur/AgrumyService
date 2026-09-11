using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises DeviceOutboxService directly - no HTTP/controller plumbing, no database (repositories are mocked).
public class DeviceOutboxServiceTests
{
    private readonly Mock<IDeviceOutboxRepository> _outbox = new(MockBehavior.Strict);
    private readonly Mock<IDeviceRepository> _devices = new(MockBehavior.Strict);
    private readonly Mock<IDeviceFarmUnitRepository> _units = new(MockBehavior.Strict);
    private readonly Mock<IFarmParcelRepository> _farmParcel = new(MockBehavior.Strict);

    private DeviceOutboxService NewService() => new(_outbox.Object, _devices.Object, _units.Object, _farmParcel.Object, new NoOpMqttCommandPublisher());

    private static Device ControllerDevice(int id) => new() { IDDevice = id, DeviceControllerEnabled = true };


    [Fact]
    public async Task Device_Target_Not_Found_Returns_TargetNotFound()
    {
        _devices.Setup(d => d.DeviceGetByIdAsync(500)).ReturnsAsync((Device?)null);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Device, 500, CommandActionType.Reboot);

        Assert.Equal(IssueCommandOutcome.TargetNotFound, result.Outcome);
        Assert.Empty(result.CreatedCommandIds);
    }

    [Fact]
    public async Task Zone_With_No_Controller_Returns_TargetNotFound()
    {
        // A zone has at most one controller - null means genuinely none, not "not looked up yet".
        _units.Setup(u => u.DeviceFarmUnitZoneGetControllerAsync(10)).ReturnsAsync((Device?)null);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Zone, 10, CommandActionType.Reboot);

        Assert.Equal(IssueCommandOutcome.TargetNotFound, result.Outcome);
    }

    [Fact]
    public async Task Zone_Target_Resolves_To_Its_Single_Controller()
    {
        DateTime before = DateTime.UtcNow;
        _units.Setup(u => u.DeviceFarmUnitZoneGetControllerAsync(10)).ReturnsAsync(ControllerDevice(500));
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.AddOutboxItemAsync(500, CommandActionType.Reboot, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);
        _outbox.Setup(c => c.MarkPublishedAsync(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Zone, 10, CommandActionType.Reboot);

        Assert.Equal(IssueCommandOutcome.Success, result.Outcome);
        Assert.Equal([1], result.CreatedCommandIds);
        _outbox.Verify(c => c.AddOutboxItemAsync(500, CommandActionType.Reboot,
            It.Is<DateTime>(d => d >= before), It.Is<DateTime>(d => d > before)), Times.Once);
    }

    [Fact]
    public async Task Unit_With_No_Controllers_Across_Any_Zone_Returns_TargetNotFound()
    {
        _units.Setup(u => u.DeviceFarmUnitGetControllersAsync(7)).ReturnsAsync(new List<Device>());

        var result = await NewService().IssueCommandAsync(CommandTargetType.Unit, 7, CommandActionType.ForceOTA);

        Assert.Equal(IssueCommandOutcome.TargetNotFound, result.Outcome);
    }

    [Fact]
    public async Task Unit_Target_FansOut_To_Every_Controller_Across_All_Its_Zones()
    {
        _units.Setup(u => u.DeviceFarmUnitGetControllersAsync(7)).ReturnsAsync(new List<Device> { ControllerDevice(500), ControllerDevice(501) });
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.ForceOTA, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(501, CommandActionType.ForceOTA, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.AddOutboxItemAsync(500, CommandActionType.ForceOTA, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);
        _outbox.Setup(c => c.AddOutboxItemAsync(501, CommandActionType.ForceOTA, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(2);
        _outbox.Setup(c => c.MarkPublishedAsync(It.IsAny<int>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Unit, 7, CommandActionType.ForceOTA);

        Assert.Equal(IssueCommandOutcome.Success, result.Outcome);
        Assert.Equal([1, 2], result.CreatedCommandIds);
    }


    [Fact]
    public async Task TenantWide_NoDevices_Returns_TargetNotFound()
    {
        _devices.Setup(d => d.DevicesGetAsync(7)).ReturnsAsync(new List<Device>());

        var result = await NewService().IssueTenantWideCommandAsync(7, CommandActionType.ForceConfigSync);

        Assert.Equal(IssueCommandOutcome.TargetNotFound, result.Outcome);
    }

    [Fact]
    public async Task TenantWide_FansOut_To_Every_Device_In_The_Tenant()
    {
        _devices.Setup(d => d.DevicesGetAsync(7)).ReturnsAsync(new List<Device> { ControllerDevice(500), ControllerDevice(501) });
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.ForceConfigSync, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(501, CommandActionType.ForceConfigSync, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.AddOutboxItemAsync(500, CommandActionType.ForceConfigSync, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(1);
        _outbox.Setup(c => c.AddOutboxItemAsync(501, CommandActionType.ForceConfigSync, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(2);
        _outbox.Setup(c => c.MarkPublishedAsync(It.IsAny<int>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var result = await NewService().IssueTenantWideCommandAsync(7, CommandActionType.ForceConfigSync);

        Assert.Equal(IssueCommandOutcome.Success, result.Outcome);
        Assert.Equal([1, 2], result.CreatedCommandIds);
    }

    [Fact]
    public async Task Device_With_Active_Command_Of_Same_ActionType_Is_Deduplicated()
    {
        _devices.Setup(d => d.DeviceGetByIdAsync(500)).ReturnsAsync(ControllerDevice(500));
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(true);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Device, 500, CommandActionType.Reboot);

        Assert.Equal(IssueCommandOutcome.AllDuplicates, result.Outcome);
        Assert.Empty(result.CreatedCommandIds);
        // Strict mock: AddOutboxItemAsync was never set up - a call to it here would throw.
    }

    /// HasActiveOutboxItemAsync is only a fast-path - a null from AddOutboxItemAsync (lost the DB-level race) must be treated as a dedup skip too.
    [Fact]
    public async Task Device_Losing_The_DB_Level_Dedup_Race_Is_Treated_As_AllDuplicates_Not_A_Crash()
    {
        _devices.Setup(d => d.DeviceGetByIdAsync(500)).ReturnsAsync(ControllerDevice(500));
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.AddOutboxItemAsync(500, CommandActionType.Reboot, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync((int?)null);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Device, 500, CommandActionType.Reboot);

        Assert.Equal(IssueCommandOutcome.AllDuplicates, result.Outcome);
        Assert.Empty(result.CreatedCommandIds);
    }

    [Fact]
    public async Task Unit_FanOut_One_Zone_Already_Pending_Is_Skipped_Not_The_Whole_Batch()
    {
        // A Unit with three controllers, one already holding an active command of this ActionType: that one is skipped, the other two still get created, outcome is still Success (not AllDuplicates).
        _units.Setup(u => u.DeviceFarmUnitGetControllersAsync(7))
            .ReturnsAsync(new List<Device> { ControllerDevice(500), ControllerDevice(501), ControllerDevice(502) });
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(true);
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(501, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(502, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.AddOutboxItemAsync(501, CommandActionType.Reboot, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(11);
        _outbox.Setup(c => c.AddOutboxItemAsync(502, CommandActionType.Reboot, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(12);
        _outbox.Setup(c => c.MarkPublishedAsync(It.IsAny<int>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Unit, 7, CommandActionType.Reboot);

        Assert.Equal(IssueCommandOutcome.Success, result.Outcome);
        Assert.Equal([11, 12], result.CreatedCommandIds);
        // Strict mock: AddOutboxItemAsync(500, ...) was never set up - device 500 must not have been added.
    }

    [Fact]
    public async Task Unit_FanOut_Every_Controller_Already_Pending_Returns_AllDuplicates()
    {
        _units.Setup(u => u.DeviceFarmUnitGetControllersAsync(7)).ReturnsAsync(new List<Device> { ControllerDevice(500), ControllerDevice(501) });
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(true);
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(501, CommandActionType.Reboot, It.IsAny<DateTime>())).ReturnsAsync(true);

        var result = await NewService().IssueCommandAsync(CommandTargetType.Unit, 7, CommandActionType.Reboot);

        Assert.Equal(IssueCommandOutcome.AllDuplicates, result.Outcome);
        Assert.Empty(result.CreatedCommandIds);
    }


    [Fact]
    public async Task GetPendingCommand_Returns_Oldest_Pending_Command()
    {
        var older = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.Reboot, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        var newer = new DeviceCommand { IDDeviceCommand = 2, DeviceID = 500, ActionType = CommandActionType.ForceOTA, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { older, newer }); // oldest first, per the repo's own contract

        PendingCommand? pending = (await NewService().GetPendingAsync(500)).Actionable;

        Assert.NotNull(pending);
        Assert.Equal(1, pending!.IDDeviceCommand);
    }

    [Fact]
    public async Task GetPendingCommand_Skips_And_Expires_Stale_Rows_Then_Returns_The_Next_Valid_One()
    {
        // A stuck expired Reboot must not hide a still-valid, newer ForceOTA command behind it.
        var expired = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.Reboot, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(-5) };
        var valid = new DeviceCommand { IDDeviceCommand = 2, DeviceID = 500, ActionType = CommandActionType.ForceOTA, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { expired, valid });
        _outbox.Setup(c => c.ExpirePendingOutboxItemsAsync(500, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        PendingCommand? pending = (await NewService().GetPendingAsync(500)).Actionable;

        Assert.NotNull(pending);
        Assert.Equal(2, pending!.IDDeviceCommand);
        _outbox.Verify(c => c.ExpirePendingOutboxItemsAsync(500, It.IsAny<DateTime>()), Times.Once);
        // Strict mock: an un-set-up SetOutboxItemStatusAsync call would throw, proving expiry is now one bulk call, not one write per expired row.
    }

    [Fact]
    public async Task GetPendingCommand_AllExpired_Returns_Null()
    {
        var expired = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.Reboot, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(-5) };
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { expired });
        _outbox.Setup(c => c.ExpirePendingOutboxItemsAsync(500, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        PendingCommand? pending = (await NewService().GetPendingAsync(500)).Actionable;

        Assert.Null(pending);
    }

    [Fact]
    public async Task GetPendingCommand_MultipleExpiredRows_ExpiresInOneBulkCall_NotOnePerRow()
    {
        var expired1 = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.Reboot, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(-10) };
        var expired2 = new DeviceCommand { IDDeviceCommand = 2, DeviceID = 500, ActionType = CommandActionType.ForceOTA, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(-5) };
        var valid = new DeviceCommand { IDDeviceCommand = 3, DeviceID = 500, ActionType = CommandActionType.ForceConfigSync, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { expired1, expired2, valid });
        _outbox.Setup(c => c.ExpirePendingOutboxItemsAsync(500, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        PendingCommand? pending = (await NewService().GetPendingAsync(500)).Actionable;

        Assert.Equal(3, pending!.IDDeviceCommand);
        _outbox.Verify(c => c.ExpirePendingOutboxItemsAsync(500, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task GetPendingCommand_NoExpiredRows_NeverCallsExpire()
    {
        var valid = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.Reboot, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { valid });
        // Strict mock: an un-set-up ExpirePendingOutboxItemsAsync call would throw, proving it's skipped when nothing needs expiring.

        PendingCommand? pending = (await NewService().GetPendingAsync(500)).Actionable;

        Assert.Equal(1, pending!.IDDeviceCommand);
    }

    /// Two poll cycles: the first poll's ack+execute must not disturb the second, still-Pending command behind it - proves FIFO holds across cycles.
    [Fact]
    public async Task Two_Commands_Are_Acked_And_Executed_In_FIFO_Order_Across_Two_Poll_Cycles()
    {
        var first = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.Reboot, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        var second = new DeviceCommand { IDDeviceCommand = 2, DeviceID = 500, ActionType = CommandActionType.ForceOTA, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };

        // poll cycle 1: device sees, acks, and executes the first command
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { first, second });
        var service = NewService();
        PendingCommand? poll1 = (await service.GetPendingAsync(500)).Actionable;
        Assert.Equal(1, poll1!.IDDeviceCommand);

        _outbox.Setup(c => c.GetOutboxItemByIdAsync(1)).ReturnsAsync(first);
        _outbox.Setup(c => c.SetOutboxItemStatusAsync(1, CommandStatus.Acknowledged, null)).Returns(Task.CompletedTask);
        await service.AcknowledgeCommandAsync(1, 500);

        _outbox.Setup(c => c.SetOutboxItemStatusAsync(1, CommandStatus.Executed, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        await service.MarkExecutedAsync(1, 500);

        // poll cycle 2: the first command is now Executed and gone from the pending list, the second surfaces next
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { second });
        PendingCommand? poll2 = (await service.GetPendingAsync(500)).Actionable;
        Assert.Equal(2, poll2!.IDDeviceCommand);

        _outbox.Verify(c => c.SetOutboxItemStatusAsync(1, CommandStatus.Acknowledged, null), Times.Once);
        _outbox.Verify(c => c.SetOutboxItemStatusAsync(1, CommandStatus.Executed, It.IsAny<DateTime>()), Times.Once);
    }


    [Fact]
    public async Task Acknowledge_Ignores_A_Command_That_Is_No_Longer_Pending()
    {
        _outbox.Setup(c => c.GetOutboxItemByIdAsync(1))
            .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, Status = CommandStatus.Acknowledged });

        await NewService().AcknowledgeCommandAsync(1, 500);

        // Strict mock: SetOutboxItemStatusAsync was never set up - a redundant ack must not call it.
    }

    [Fact]
    public async Task MarkExecuted_Ignores_An_Unknown_CommandId()
    {
        _outbox.Setup(c => c.GetOutboxItemByIdAsync(999)).ReturnsAsync((DeviceCommand?)null);

        await NewService().MarkExecutedAsync(999, 500);
    }

    [Fact]
    public async Task MarkExecuted_Accepts_Pending_Directly_Covering_Reboot_With_No_Prior_Ack()
    {
        // Reboot has nothing to ack-then-execute on the same connection - MarkExecutedAsync must accept a straight Pending -> Executed transition.
        _outbox.Setup(c => c.GetOutboxItemByIdAsync(1))
            .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, Status = CommandStatus.Pending, ActionType = CommandActionType.Reboot });
        _outbox.Setup(c => c.SetOutboxItemStatusAsync(1, CommandStatus.Executed, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        DeviceCommand? executed = await NewService().MarkExecutedAsync(1, 500);

        _outbox.Verify(c => c.SetOutboxItemStatusAsync(1, CommandStatus.Executed, It.IsAny<DateTime>()), Times.Once);
        Assert.Equal(CommandActionType.Reboot, executed?.ActionType);
    }

    [Fact]
    public async Task MarkExecuted_ReturnsNull_ForACommandBelongingToADifferentDevice()
    {
        // DeviceApiController.PushEvent branches on the returned command's ActionType - a rejected mark-executed must not hand back a command from a different device.
        _outbox.Setup(c => c.GetOutboxItemByIdAsync(1))
            .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 1, DeviceID = 501, Status = CommandStatus.Pending, ActionType = CommandActionType.DetectSensors });

        DeviceCommand? executed = await NewService().MarkExecutedAsync(1, 500);

        Assert.Null(executed);
    }

    [Fact]
    public async Task Acknowledge_Ignores_A_Command_Belonging_To_A_Different_Device()
    {
        // device 500 must not be able to ack a Pending command that belongs to device 501.
        _outbox.Setup(c => c.GetOutboxItemByIdAsync(1))
            .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 1, DeviceID = 501, Status = CommandStatus.Pending });

        await NewService().AcknowledgeCommandAsync(1, 500);

        // Strict mock: SetOutboxItemStatusAsync was never set up - a cross-device ack must not call it.
    }

    [Fact]
    public async Task MarkExecuted_Ignores_A_Command_Belonging_To_A_Different_Device()
    {
        // device 500 must not be able to mark device 501's command Executed.
        _outbox.Setup(c => c.GetOutboxItemByIdAsync(1))
            .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 1, DeviceID = 501, Status = CommandStatus.Pending });

        await NewService().MarkExecutedAsync(1, 500);

        // Strict mock: SetOutboxItemStatusAsync was never set up - a cross-device execute confirmation must not call it.
    }

    [Fact]
    public async Task IssueWifiUpdate_Success_PublishesPayloadCarryingCommand()
    {
        DateTime before = DateTime.UtcNow;
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.AddOutboxItemAsync(500, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.Is<string?>(p => p != null && p.Contains("NewSsid") && p.Contains("NewPass")))).ReturnsAsync(9);
        _devices.Setup(d => d.DeviceGetByIdAsync(500)).ReturnsAsync(ControllerDevice(500));
        _outbox.Setup(c => c.MarkPublishedAsync(9, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var result = await NewService().IssueWifiUpdateCommandAsync(500, "NewSsid", "NewPass");

        Assert.Equal(IssueCommandOutcome.Success, result.Outcome);
        Assert.Equal([9], result.CreatedCommandIds);
        _outbox.Verify(c => c.AddOutboxItemAsync(500, CommandActionType.UpdateWifiCredentials,
            It.Is<DateTime>(d => d >= before), It.Is<DateTime>(d => d > before), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task IssueWifiUpdate_AlreadyPendingForDevice_ReturnsAllDuplicates()
    {
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>())).ReturnsAsync(true);

        var result = await NewService().IssueWifiUpdateCommandAsync(500, "NewSsid", "NewPass");

        Assert.Equal(IssueCommandOutcome.AllDuplicates, result.Outcome);
        Assert.Empty(result.CreatedCommandIds);
        // Strict mock: AddOutboxItemAsync was never set up - a call to it here would throw.
    }

    [Fact]
    public async Task IssueWifiUpdate_LosingTheDbLevelDedupRace_IsTreatedAsAllDuplicates_NotACrash()
    {
        _outbox.Setup(c => c.HasActiveOutboxItemAsync(500, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>())).ReturnsAsync(false);
        _outbox.Setup(c => c.AddOutboxItemAsync(500, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null);

        var result = await NewService().IssueWifiUpdateCommandAsync(500, "NewSsid", "NewPass");

        Assert.Equal(IssueCommandOutcome.AllDuplicates, result.Outcome);
        Assert.Empty(result.CreatedCommandIds);
    }

    private static string ProvisionPayloadJson(string discoveredApMac, string? deviceName = null, int? zoneId = null) =>
        System.Text.Json.JsonSerializer.Serialize(new DiscoveryProvisionPayload
        {
            Username = "admin@example.com",
            Pin = "ABC123",
            DiscoveredApMac = discoveredApMac,
            Ssid = "TestWifi",
            WifiPassword = "TestPass",
            DeviceName = deviceName,
            ZoneID = zoneId,
        });

    [Fact]
    public async Task ConsumePendingProvision_MatchingMac_ReturnsPayload_AndMarksExecuted()
    {
        var command = new DeviceCommand { IDDeviceCommand = 7, Status = CommandStatus.Acknowledged, Payload = ProvisionPayloadJson("AABBCCDDEEFF", "MySensor", 42) };
        _outbox.Setup(c => c.GetActiveProvisionCommandsAsync()).ReturnsAsync(new List<DeviceCommand> { command });
        _outbox.Setup(c => c.SetOutboxItemStatusAsync(7, CommandStatus.Executed, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        DiscoveryProvisionPayload? result = await NewService().ConsumePendingProvisionAsync("aabbccddeeff");

        Assert.NotNull(result);
        Assert.Equal("MySensor", result!.DeviceName);
        Assert.Equal(42, result.ZoneID);
    }

    [Fact]
    public async Task ConsumePendingProvision_NoMatchingMac_ReturnsNull()
    {
        var command = new DeviceCommand { IDDeviceCommand = 7, Status = CommandStatus.Acknowledged, Payload = ProvisionPayloadJson("112233445566") };
        _outbox.Setup(c => c.GetActiveProvisionCommandsAsync()).ReturnsAsync(new List<DeviceCommand> { command });

        DiscoveryProvisionPayload? result = await NewService().ConsumePendingProvisionAsync("AABBCCDDEEFF");

        Assert.Null(result);
        // Strict mock: SetOutboxItemStatusAsync was never set up - a non-matching mac must not consume the command.
    }

    [Fact]
    public async Task ConsumePendingProvision_MalformedPayload_SkippedNotThrown()
    {
        var malformed = new DeviceCommand { IDDeviceCommand = 8, Status = CommandStatus.Pending, Payload = "not json" };
        _outbox.Setup(c => c.GetActiveProvisionCommandsAsync()).ReturnsAsync(new List<DeviceCommand> { malformed });

        DiscoveryProvisionPayload? result = await NewService().ConsumePendingProvisionAsync("AABBCCDDEEFF");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingAsync_SeparatesConfigChangedAndHardResetFromTheActionableCommand()
    {
        // Order matters for FIFO of the actionable candidate, but ConfigChanged/HardReset are pulled out regardless of position - they never become the response's PendingCommand.
        var configChanged = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.ConfigChanged, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddDays(30) };
        var hardReset = new DeviceCommand { IDDeviceCommand = 2, DeviceID = 500, ActionType = CommandActionType.HardReset, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddDays(365) };
        var reboot = new DeviceCommand { IDDeviceCommand = 3, DeviceID = 500, ActionType = CommandActionType.Reboot, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { configChanged, hardReset, reboot });

        PendingItems pending = await NewService().GetPendingAsync(500);

        Assert.NotNull(pending.Actionable);
        Assert.Equal(3, pending.Actionable!.IDDeviceCommand);
        Assert.True(pending.ConfigChangePending);
        Assert.True(pending.HardResetPending);
    }

    [Fact]
    public async Task EnqueueHardReset_Then_ConsumeIfPending_IsFireOnce()
    {
        DateTime before = DateTime.UtcNow;
        _outbox.Setup(c => c.AddOutboxItemAsync(500, CommandActionType.HardReset, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(1);
        _devices.Setup(d => d.DeviceGetByIdAsync(500)).ReturnsAsync(ControllerDevice(500));
        _outbox.Setup(c => c.MarkPublishedAsync(1, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var service = NewService();
        await service.EnqueueHardResetAsync(500);
        _outbox.Verify(c => c.AddOutboxItemAsync(500, CommandActionType.HardReset,
            It.Is<DateTime>(d => d >= before), It.Is<DateTime>(d => d > before.AddDays(360)), null), Times.Once);

        // First observation (e.g. the anonymous HardResetPending endpoint) sees it pending and consumes it.
        var pendingRow = new DeviceCommand { IDDeviceCommand = 1, DeviceID = 500, ActionType = CommandActionType.HardReset, Status = CommandStatus.Pending, ExpiresAt = DateTime.UtcNow.AddDays(365) };
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand> { pendingRow });
        _outbox.Setup(c => c.ConsumePendingByTypeAsync(500, CommandActionType.HardReset, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        bool first = await service.ConsumeHardResetIfPendingAsync(500);
        Assert.True(first);
        _outbox.Verify(c => c.ConsumePendingByTypeAsync(500, CommandActionType.HardReset, It.IsAny<DateTime>()), Times.Once);

        // Second observation (e.g. the next ordinary poll) sees nothing left pending - fire-once.
        _outbox.Setup(c => c.GetPendingOutboxItemsAsync(500)).ReturnsAsync(new List<DeviceCommand>());
        bool second = await service.ConsumeHardResetIfPendingAsync(500);
        Assert.False(second);
        // Strict mock: ConsumePendingByTypeAsync was set up for exactly one call above - a second call here would fail the Times.Once verification, not throw, so the real proof is the mock's own call count already checked.
    }
}
