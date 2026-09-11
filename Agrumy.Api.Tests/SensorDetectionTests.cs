using System.Text.Json;
using Agrumy.Shared;
using Agrumy.Api.Commands;
using Agrumy.Api.Controllers.API;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Agrumy.Api.Tests.TestSupport;

namespace Agrumy.Api.Tests;

/// DeviceSensorDetectionResult's JSON shape and the DeviceApiController.PushEvent branch that persists it - the actual I2C scan is firmware-side and untestable here.
public class SensorDetectionTests
{
    [Fact]
    public void DeviceSensorDetectionResult_RoundTrips_MatchingFirmwareWireShape()
    {
        // {"Addresses":[{"Address":118,"Candidates":[1005,1004]}]} - PascalCase, same convention ServiceController::pushEvent already uses for its own top-level fields (EventType/Message/CommandId).
        const string wireJson = "{\"Addresses\":[{\"Address\":118,\"Candidates\":[1005,1004]}]}";

        var result = JsonSerializer.Deserialize<DeviceSensorDetectionResult>(wireJson);

        Assert.NotNull(result);
        Assert.Single(result!.Addresses);
        Assert.Equal(118, result.Addresses[0].Address);
        Assert.Equal([1005, 1004], result.Addresses[0].Candidates);
    }

    private readonly Mock<IAllFacetsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();

    private DeviceApiController NewController()
    {
        var catalog = FirmwareTestSupport.NewCatalog(_repo.Object, _repo.Object, _repo.Object);
        var outboxService = new DeviceOutboxService(_repo.Object, _repo.Object, _repo.Object, _repo.Object, new NoOpMqttCommandPublisher());
        var controller = new DeviceApiController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object,
            outboxService, catalog,
            new Agrumy.Api.Devices.DeviceConfigBuilder(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, catalog, outboxService),
            Microsoft.Extensions.Options.Options.Create(new AgrumySettings()), NullLogger<DeviceApiController>.Instance,
            new Agrumy.Api.Quota.TenantQuotaEnforcer(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.HttpContext.Items[DeviceAuth.ApiIdItemKey] = "api-guid";
        return controller;
    }

    // The firmware pushes these EventType strings literally (ServiceController::pushEvent call sites) - PushEvent's `Enum.TryParse<DeviceEventType>` rejects anything not in the enum with a 400, so a string used on the firmware side but never added here (SensorMissing was, once) silently 400s every push forever.
    [Theory]
    [InlineData("NoInternet")]
    [InlineData("ConfigSyncFailed")]
    [InlineData("ConfigApplied")]
    [InlineData("CrashLoopRollback")]
    [InlineData("OtaFailed")]
    [InlineData("BufferDiscarded")]
    [InlineData("CommandExecuted")]
    [InlineData("SafetyLimitTripped")]
    [InlineData("Crash")]
    [InlineData("I2CFault")]
    [InlineData("RuleRejected")]
    [InlineData("SensorStale")]
    [InlineData("LowMemoryReboot")]
    [InlineData("LoRaHardwareNotDetected")]
    [InlineData("SensorMissing")]
    public async Task PushEvent_EveryFirmwareEventTypeString_IsAKnownDeviceEventType(string eventType)
    {
        _repo.Setup(r => r.DeviceGetByApiIdAsync("api-guid")).ReturnsAsync(new Device { IDDevice = 500, TenantID = 3 });
        _repo.Setup(r => r.EventDevicePushAsync(500, 3, It.IsAny<DeviceEventType>(), It.IsAny<string?>())).ReturnsAsync(true);

        var result = await NewController().PushEvent(new DeviceEventPush { EventType = eventType, Message = "x" });

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task PushEvent_CommandExecuted_ForDetectSensors_PersistsParsedResult()
    {
        _repo.Setup(r => r.DeviceGetByApiIdAsync("api-guid")).ReturnsAsync(new Device { IDDevice = 500, TenantID = 3 });
        _repo.Setup(r => r.EventDevicePushAsync(500, 3, DeviceEventType.CommandExecuted, It.IsAny<string?>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetOutboxItemByIdAsync(9))
             .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 9, DeviceID = 500, ActionType = CommandActionType.DetectSensors, Status = CommandStatus.Acknowledged });
        _repo.Setup(r => r.SetOutboxItemStatusAsync(9, CommandStatus.Executed, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeviceSensorDetectionResultSetAsync(500,
                It.Is<string?>(json => json != null && json.Contains("1005")), It.IsAny<DateTimeOffset>()))
             .Returns(Task.CompletedTask);

        var result = await NewController().PushEvent(new DeviceEventPush
        {
            EventType = "CommandExecuted",
            CommandId = 9,
            Message = "{\"Addresses\":[{\"Address\":118,\"Candidates\":[1005]}]}",
        });

        Assert.IsType<OkResult>(result);
        _repo.Verify(r => r.DeviceSensorDetectionResultSetAsync(500, It.IsAny<string?>(), It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task PushEvent_CommandExecuted_ForDetectSensors_MalformedJson_NoOpsInsteadOfThrowing()
    {
        _repo.Setup(r => r.DeviceGetByApiIdAsync("api-guid")).ReturnsAsync(new Device { IDDevice = 500, TenantID = 3 });
        _repo.Setup(r => r.EventDevicePushAsync(500, 3, DeviceEventType.CommandExecuted, It.IsAny<string?>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetOutboxItemByIdAsync(9))
             .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 9, DeviceID = 500, ActionType = CommandActionType.DetectSensors, Status = CommandStatus.Acknowledged });
        _repo.Setup(r => r.SetOutboxItemStatusAsync(9, CommandStatus.Executed, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        // Strict mock: DeviceSensorDetectionResultSetAsync deliberately NOT set up - a malformed payload must never reach it.

        var result = await NewController().PushEvent(new DeviceEventPush
        {
            EventType = "CommandExecuted",
            CommandId = 9,
            Message = "not valid json",
        });

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task PushEvent_CommandExecuted_ForOtherActionType_NeverTouchesSensorDetection()
    {
        _repo.Setup(r => r.DeviceGetByApiIdAsync("api-guid")).ReturnsAsync(new Device { IDDevice = 500, TenantID = 3 });
        _repo.Setup(r => r.EventDevicePushAsync(500, 3, DeviceEventType.CommandExecuted, It.IsAny<string?>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetOutboxItemByIdAsync(9))
             .ReturnsAsync(new DeviceCommand { IDDeviceCommand = 9, DeviceID = 500, ActionType = CommandActionType.ForceConfigSync, Status = CommandStatus.Acknowledged });
        _repo.Setup(r => r.SetOutboxItemStatusAsync(9, CommandStatus.Executed, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        // Strict mock: DeviceSensorDetectionResultSetAsync not set up - an unrelated command's CommandExecuted must not call it.

        var result = await NewController().PushEvent(new DeviceEventPush
        {
            EventType = "CommandExecuted",
            CommandId = 9,
            Message = "config already current from this poll",
        });

        Assert.IsType<OkResult>(result);
    }
}
