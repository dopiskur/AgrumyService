using System.Security.Claims;
using Agrumy.Api.Controllers.API;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Agrumy.Api.Tests.TestSupport;

namespace Agrumy.Api.Tests;

/// Recycle Bin listing/restore ownership checks.
public class RecycleBinApiControllerTests
{
    private readonly Mock<IAllFacetsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();

    private RecycleBinApiController NewController()
    {
        var controller = new RecycleBinApiController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object);
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
    public async Task DeviceRestore_NotFound_Returns404()
    {
        _repo.Setup(r => r.DeviceRecycleBinGetByIdAsync(8)).ReturnsAsync((Device?)null);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeviceRestore(8);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeviceRestore_ForeignTenant_Returns403_NeverRestores()
    {
        _repo.Setup(r => r.DeviceRecycleBinGetByIdAsync(8)).ReturnsAsync(new Device { IDDevice = 8, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeviceRestore(8);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        // MockBehavior.Strict: DeviceRestoreAsync has no setup, proving a foreign-tenant device was never restored.
    }

    [Fact]
    public async Task DeviceRestore_OwnTenant_RestoresAndAudits()
    {
        _repo.Setup(r => r.DeviceRecycleBinGetByIdAsync(8)).ReturnsAsync(new Device { IDDevice = 8, TenantID = 1, DeviceName = "D1" });
        _repo.Setup(r => r.DeviceRestoreAsync(8, 1)).ReturnsAsync(true);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeviceRestore(8);

        Assert.True(result.Value);
    }

    [Fact]
    public async Task FarmRestore_ForeignTenant_Returns403_NeverRestores()
    {
        _repo.Setup(r => r.DeviceFarmRecycleBinGetByIdAsync(5)).ReturnsAsync(new DeviceFarm { IDDeviceFarm = 5, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.FarmRestore(5);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        // MockBehavior.Strict: DeviceFarmRestoreAsync has no setup, proving a foreign-tenant farm was never restored.
    }

    [Fact]
    public async Task FarmRestore_NotFound_Returns404()
    {
        _repo.Setup(r => r.DeviceFarmRecycleBinGetByIdAsync(5)).ReturnsAsync((DeviceFarm?)null);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.FarmRestore(5);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
