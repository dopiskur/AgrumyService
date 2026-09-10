using System.Security.Claims;
using Agrumy.Api.Controllers.API;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Tests.TestSupport;
using Agrumy.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Agrumy.Api.Tests;

/// SensorDataODataController.Get - the [EnableQuery]/OData query-string composition itself is exercised by ASP.NET Core's own OData middleware (not re-tested here); this covers the gating this controller adds on top: disabled feed, no-tenant caller, and that an enabled+tenant-scoped call reaches the tenant-filtered IQueryable.
public class SensorDataODataControllerTests
{
    private readonly Mock<IAllFacetsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();

    private SensorDataODataController NewController(IEnumerable<Claim>? claims = null)
    {
        var controller = new SensorDataODataController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims ?? [])) },
        };
        return controller;
    }

    [Fact]
    public async Task Disabled_Feed_Returns_NotFound_Even_For_A_Valid_Tenant_Caller()
    {
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { ODataEnabled = false });

        var result = await NewController([new Claim("TenantID", "1")]).Get();

        Assert.IsType<NotFoundResult>(result.Result);
        // Strict mock: SensorDataODataQueryable would throw if called - a disabled feed never reaches the repository.
    }

    [Fact]
    public async Task Enabled_Feed_Without_A_TenantID_Claim_Is_Forbidden()
    {
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { ODataEnabled = true });

        var result = await NewController().Get();

        Assert.IsType<ForbidResult>(result.Result);
        // Strict mock: SensorDataODataQueryable would throw if called.
    }

    [Fact]
    public async Task Enabled_Feed_With_A_Tenant_Returns_That_Tenants_Queryable()
    {
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig { ODataEnabled = true });
        var expected = new[] { new SensorDataODataEntry { IDSensorData = 1, DeviceID = 42 } }.AsQueryable();
        _repo.Setup(r => r.SensorDataODataQueryable(7)).Returns(expected);

        var result = await NewController([new Claim("TenantID", "7")]).Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }
}
