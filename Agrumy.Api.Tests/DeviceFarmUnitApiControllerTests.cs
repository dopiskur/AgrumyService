using System.Security.Claims;
using Agrumy.Shared;
using Agrumy.Api.Commands;
using Agrumy.Api.Controllers.API;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace Agrumy.Api.Tests;

/// Roadmap #411 - bulk WiFi switch fan-out across every device in a unit.
public class DeviceFarmUnitApiControllerTests
{
    private readonly Mock<IRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();

    private DeviceFarmUnitApiController NewController()
    {
        var controller = new DeviceFarmUnitApiController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object,
            Options.Create(new AgrumySettings()), new ManualActuateService(_repo.Object),
            new CommandQueueService(_repo.Object, _repo.Object, _repo.Object, new NoOpMqttCommandPublisher()));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static void SetCaller(ControllerBase controller, int tenantId, params string[] roles)
    {
        var claims = new List<Claim> { new("TenantID", tenantId.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
    }

    [Fact]
    public async Task UnitWifiUpdate_IssuesToEveryDevice_SkippingOnesAlreadyPending()
    {
        _repo.Setup(r => r.DeviceFarmUnitGetByIdAsync(5)).ReturnsAsync(new DeviceFarmUnit { IDDeviceFarmUnit = 5, TenantID = 1 });
        _repo.Setup(r => r.DeviceFarmUnitGetDevicesAsync(5)).ReturnsAsync(new List<Device>
        {
            new() { IDDevice = 10, DeviceName = "A" },
            new() { IDDevice = 11, DeviceName = "B" },
        });
        _repo.Setup(r => r.HasActiveCommandAsync(10, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>())).ReturnsAsync(false);
        _repo.Setup(r => r.AddCommandAsync(10, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>())).ReturnsAsync(101);
        _repo.Setup(r => r.DeviceGetByIdAsync(10)).ReturnsAsync(new Device { IDDevice = 10, ApiId = "a1" });
        _repo.Setup(r => r.HasActiveCommandAsync(11, CommandActionType.UpdateWifiCredentials, It.IsAny<DateTime>())).ReturnsAsync(true);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.UnitWifiUpdate(5, new UnitWifiUpdateRequest { Ssid = "NewSsid", WifiPassword = "pw" });

        UnitWifiUpdateResult body = Assert.IsType<UnitWifiUpdateResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, body.DeviceCount);
        Assert.Equal(1, body.IssuedCount);
        Assert.True(Assert.Single(body.Devices, d => d.IDDevice == 10).Issued);
        Assert.False(Assert.Single(body.Devices, d => d.IDDevice == 11).Issued);
    }

    [Fact]
    public async Task UnitWifiUpdate_ForeignTenant_Returns403_NeverIssues()
    {
        _repo.Setup(r => r.DeviceFarmUnitGetByIdAsync(5)).ReturnsAsync(new DeviceFarmUnit { IDDeviceFarmUnit = 5, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.UnitWifiUpdate(5, new UnitWifiUpdateRequest { Ssid = "NewSsid", WifiPassword = "pw" });

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        // MockBehavior.Strict: DeviceFarmUnitGetDevicesAsync has no setup, proving nothing was issued for a foreign tenant's unit.
    }

    [Fact]
    public async Task UnitWifiUpdate_MissingSsid_Returns400()
    {
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.UnitWifiUpdate(5, new UnitWifiUpdateRequest { Ssid = "", WifiPassword = "pw" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // Roadmap #238 - dashboard widget list validation/ownership on the save endpoint.
    [Fact]
    public async Task DeviceFarmUnitZoneWidgetsSet_ForeignTenant_Returns403_NeverSaves()
    {
        _repo.Setup(r => r.DeviceFarmUnitZoneGetByIdAsync(7)).ReturnsAsync(new DeviceFarmUnitZone { IDDeviceFarmUnitZone = 7, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeviceFarmUnitZoneWidgetsSet(7, [new DashboardWidget { Type = DashboardWidgetType.Text, Label = "Hi" }]);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        // MockBehavior.Strict: DeviceFarmUnitZoneWidgetsSetAsync has no setup, proving nothing was saved for a foreign tenant's zone.
    }

    [Fact]
    public async Task DeviceFarmUnitZoneWidgetsSet_TextWidgetWithNoLabel_Returns400()
    {
        _repo.Setup(r => r.DeviceFarmUnitZoneGetByIdAsync(7)).ReturnsAsync(new DeviceFarmUnitZone { IDDeviceFarmUnitZone = 7, TenantID = 1 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeviceFarmUnitZoneWidgetsSet(7, [new DashboardWidget { Type = DashboardWidgetType.Text, Label = "  " }]);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeviceFarmUnitZoneWidgetsSet_TooMany_Returns400()
    {
        _repo.Setup(r => r.DeviceFarmUnitZoneGetByIdAsync(7)).ReturnsAsync(new DeviceFarmUnitZone { IDDeviceFarmUnitZone = 7, TenantID = 1 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);
        var widgets = Enumerable.Range(0, 21).Select(_ => new DashboardWidget { Type = DashboardWidgetType.SensorValue, Metric = SensorMetric.Temperature }).ToList();

        var result = await controller.DeviceFarmUnitZoneWidgetsSet(7, widgets);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeviceFarmUnitZoneWidgetsSet_Valid_SavesAndAudits()
    {
        _repo.Setup(r => r.DeviceFarmUnitZoneGetByIdAsync(7)).ReturnsAsync(new DeviceFarmUnitZone { IDDeviceFarmUnitZone = 7, TenantID = 1 });
        var widgets = new List<DashboardWidget> { new() { Type = DashboardWidgetType.SensorValue, Metric = SensorMetric.Temperature } };
        _repo.Setup(r => r.DeviceFarmUnitZoneWidgetsSetAsync(7, widgets)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeviceFarmUnitZoneWidgetsSet(7, widgets);

        Assert.True(result.Value);
    }
}
