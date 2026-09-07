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

/// Covers the roadmap #268 WiFi-network management endpoints - password visibility by role, and
/// ownership checks on cross-tenant Update/Delete.
public class DiscoveryWifiConfigTests
{
    private readonly Mock<IRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();

    private DiscoveryApiController NewController(int? tenantId, params string[] roles)
    {
        var controller = new DiscoveryApiController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object,
            new CommandQueueService(_repo.Object, _repo.Object, _repo.Object, new NoOpMqttCommandPublisher()), Options.Create(new AgrumySettings()));
        var claims = new List<Claim> { new("TenantID", tenantId?.ToString() ?? "") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };
        return controller;
    }

    [Fact]
    public async Task WifiConfigs_TenantReader_PasswordStripped()
    {
        _repo.Setup(r => r.TenantWifiConfigsGetAsync(1)).ReturnsAsync(new List<TenantWifiConfig>
        {
            new() { IDTenantWifiConfig = 1, TenantID = 1, Ssid = "HomeWifi", Password = "secret" },
        });

        var result = await NewController(1, RoleNames.TenantReader).WifiConfigs();

        var configs = Assert.IsType<List<TenantWifiConfig>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(Assert.Single(configs).Password);
    }

    /// Write-only, roadmap #395(6) - Password is never returned to ANY caller, DeviceManagers included, so a saved WiFi password can't leak just by loading the list/edit page.
    [Fact]
    public async Task WifiConfigs_TenantAdmin_PasswordAlsoStripped()
    {
        _repo.Setup(r => r.TenantWifiConfigsGetAsync(1)).ReturnsAsync(new List<TenantWifiConfig>
        {
            new() { IDTenantWifiConfig = 1, TenantID = 1, Ssid = "HomeWifi", Password = "secret" },
        });

        var result = await NewController(1, RoleNames.TenantAdmin).WifiConfigs();

        var configs = Assert.IsType<List<TenantWifiConfig>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(Assert.Single(configs).Password);
    }

    [Fact]
    public async Task WifiConfigAdd_IgnoresBodyTenantId_UsesCallers()
    {
        TenantWifiConfig? captured = null;
        _repo.Setup(r => r.TenantWifiConfigAddAsync(It.IsAny<TenantWifiConfig>()))
            .Callback<TenantWifiConfig>(c => captured = c)
            .ReturnsAsync((TenantWifiConfig c) => c);

        var result = await NewController(1, RoleNames.TenantAdmin).WifiConfigAdd(new TenantWifiConfig { TenantID = 999, Ssid = "NewNet" });

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, captured!.TenantID);
    }

    [Fact]
    public async Task WifiConfigUpdate_DifferentTenant_Returns403()
    {
        _repo.Setup(r => r.TenantWifiConfigGetByIdAsync(5))
            .ReturnsAsync(new TenantWifiConfig { IDTenantWifiConfig = 5, TenantID = 2, Ssid = "OtherTenantNet" });

        var result = await NewController(1, RoleNames.TenantAdmin).WifiConfigUpdate(5, new TenantWifiConfig { Ssid = "Hijacked" });

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
        // Strict mock: TenantWifiConfigUpdateAsync was never set up - a cross-tenant edit must not reach it.
    }

    [Fact]
    public async Task WifiConfigDelete_NotFound_Returns404()
    {
        _repo.Setup(r => r.TenantWifiConfigGetByIdAsync(5)).ReturnsAsync((TenantWifiConfig?)null);

        var result = await NewController(1, RoleNames.TenantAdmin).WifiConfigDelete(5);

        Assert.IsType<NotFoundResult>(result);
    }
}
