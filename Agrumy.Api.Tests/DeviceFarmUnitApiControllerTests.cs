using System.Security.Claims;
using api;
using api.Commands;
using api.Controllers.API;
using api.Dal.Interface;
using api.Models;
using api.Security;
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
}
