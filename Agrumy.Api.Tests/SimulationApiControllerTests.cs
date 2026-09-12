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

/// "Add Simulation" sessions: a device (physical or virtual) is added TO a named, time-boxed session instead of being independently toggled.
public class SimulationApiControllerTests
{
    private readonly Mock<IAllFacetsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new(MockBehavior.Strict);

    // These tests aren't about quota behavior - every tenant is unlimited by default here.
    public SimulationApiControllerTests() => _repo.Setup(r => r.TenantQuotaGetAsync(It.IsAny<int>())).ReturnsAsync((TenantQuota?)null);

    private SimulationApiController NewController()
    {
        var controller = new SimulationApiController(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _cache.Object, _httpClientFactory.Object,
            new Agrumy.Api.Quota.TenantQuotaEnforcer(_repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object, _repo.Object),
            new Agrumy.Api.Devices.RuleValidationService(_repo.Object),
            Microsoft.Extensions.Options.Options.Create(new Agrumy.Shared.AgrumySettings()));
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
    public async Task CreateSession_MissingName_Returns400_NeverWrites()
    {
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.CreateSession(new SimulationSessionCreateRequest { Name = "" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        // MockBehavior.Strict: SimulationSessionAddAsync has no setup, proving nothing was created.
    }

    // Create no longer takes a duration; the session is born with no StartedAtUtc/ExpiresAtUtc at all.
    [Fact]
    public async Task CreateSession_Valid_PersistsWithNoStartedOrExpiry()
    {
        SimulationSession? saved = null;
        _repo.Setup(r => r.SimulationSessionAddAsync(It.IsAny<SimulationSession>(), It.IsAny<Func<Task<string?>>?>()))
             .Callback<SimulationSession, Func<Task<string?>>?>((s, _) => saved = s)
             .ReturnsAsync((SimulationSession s, Func<Task<string?>>? _) => { s.IDSimulationSession = 42; return s; });
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.CreateSession(new SimulationSessionCreateRequest { Name = "Irrigation test" });

        var created = Assert.IsType<SimulationSession>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(42, created.IDSimulationSession);
        Assert.Equal("Irrigation test", saved!.Name);
        Assert.Equal(1, saved.TenantID);
        Assert.Null(saved.StartedAtUtc);
        Assert.Null(saved.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2881)]
    public async Task StartSession_DurationOutOfRange_Returns400_NeverWrites(int minutes)
    {
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.StartSession(5, new SimulationSessionStartRequest { DurationMinutes = minutes });

        Assert.IsType<BadRequestObjectResult>(result);
        // MockBehavior.Strict: SimulationSessionGetByIdAsync has no setup, proving duration was rejected before even loading the session.
    }

    [Fact]
    public async Task StartSession_NeverStarted_Starts()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession { IDSimulationSession = 5, TenantID = 1, Name = "Test" });
        _repo.Setup(r => r.SimulationSessionStartAsync(5, 1440)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.StartSession(5, new SimulationSessionStartRequest { DurationMinutes = 1440 });

        Assert.IsType<OkResult>(result);
    }

    // The same action resumes a Stopped/expired session; only a CURRENTLY running one rejects it.
    [Fact]
    public async Task StartSession_PreviouslyStopped_Resumes()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, Name = "Test",
            StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-2), ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1), StoppedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
        });
        _repo.Setup(r => r.SimulationSessionStartAsync(5, 60)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.StartSession(5, new SimulationSessionStartRequest { DurationMinutes = 60 });

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task StartSession_CurrentlyRunning_ReturnsConflict_NeverRestarts()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, Name = "Test",
            StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.StartSession(5, new SimulationSessionStartRequest { DurationMinutes = 60 });

        Assert.IsType<ConflictObjectResult>(result);
        // MockBehavior.Strict: SimulationSessionStartAsync has no setup, proving a running session was never restarted out from under itself.
    }

    [Fact]
    public async Task DeleteSession_TurnsOffPhysicalMembers_ThenDeletes()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, Name = "Test",
            Devices = [new DeviceDto { IDDevice = 8 }],
        });
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(1)).ReturnsAsync(new List<int>());
        _repo.Setup(r => r.DeviceSimulationSetAsync(8, It.Is<DeviceSimulation>(s => s.Enabled == false))).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SimulationSessionDeleteAsync(5)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeleteSession(5);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteSession_VirtualMember_CascadesToRecycleBin_NeverJustUnlinked()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, Name = "Test",
            Devices = [new DeviceDto { IDDevice = 9 }],
        });
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(1)).ReturnsAsync(new List<int> { 9 });
        _repo.Setup(r => r.VirtualDeviceDeleteAsync(9, 1)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SimulationSessionDeleteAsync(5)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeleteSession(5);

        Assert.IsType<OkResult>(result);
        // MockBehavior.Strict: DeviceSimulationSetAsync has no setup, proving the virtual member went through VirtualDeviceDeleteAsync (soft-delete/Recycle Bin) instead of the physical-override "turn off" path.
    }

    [Fact]
    public async Task DeleteSession_ForeignTenant_Returns403_NeverDeletes()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession { IDSimulationSession = 5, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.DeleteSession(5);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        // MockBehavior.Strict: SimulationSessionDeleteAsync has no setup, proving a foreign tenant's session was never deleted.
    }

    [Fact]
    public async Task GetSession_ForeignTenant_Returns403()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession { IDSimulationSession = 5, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.GetSession(5);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task GetSession_NotFound_Returns404()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync((SimulationSession?)null);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.GetSession(5);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task AddDeviceToSession_SessionAlreadyEnded_Returns400_NeverAdds()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1), // already expired
        });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.AddDeviceToSession(5, 8);

        Assert.IsType<BadRequestObjectResult>(result);
        // MockBehavior.Strict: DeviceGetByIdAsync/SimulationSessionDeviceAddAsync have no setup, proving the ended session was rejected first.
    }

    [Fact]
    public async Task AddDeviceToSession_ForeignTenantSession_Returns403_NeverAdds()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 99, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.AddDeviceToSession(5, 8);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        // MockBehavior.Strict: DeviceGetByIdAsync/SimulationSessionDeviceAddAsync have no setup, proving a caller's own device was never added to another tenant's session.
    }

    [Fact]
    public async Task AddDeviceToSession_DeviceBusyInAnotherSession_ReturnsConflict()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        _repo.Setup(r => r.DeviceGetByIdAsync(8)).ReturnsAsync(new Device { IDDevice = 8, TenantID = 1 });
        _repo.Setup(r => r.SimulationSessionDeviceAddAsync(5, 8)).ReturnsAsync(false);
        _repo.Setup(r => r.DeviceActiveSimulationSessionIdGetAsync(8)).ReturnsAsync(3);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.AddDeviceToSession(5, 8);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("3", conflict.Value!.ToString());
    }

    [Fact]
    public async Task AddDeviceToSession_PhysicalDevice_EnablesSensorOverride()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        _repo.Setup(r => r.DeviceGetByIdAsync(8)).ReturnsAsync(new Device { IDDevice = 8, TenantID = 1 });
        _repo.Setup(r => r.SimulationSessionDeviceAddAsync(5, 8)).ReturnsAsync(true);
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(1)).ReturnsAsync(new List<int>()); // not virtual
        _repo.Setup(r => r.DeviceSimulationGetAsync(8)).ReturnsAsync((DeviceSimulation?)null);
        DeviceSimulation? set = null;
        _repo.Setup(r => r.DeviceSimulationSetAsync(8, It.IsAny<DeviceSimulation>()))
             .Callback<int, DeviceSimulation>((_, s) => set = s)
             .Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.AddDeviceToSession(5, 8);

        Assert.IsType<OkResult>(result);
        Assert.True(set!.Enabled);
    }

    [Fact]
    public async Task AddDeviceToSession_VirtualDevice_NeverTouchesDeviceSimulation()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        _repo.Setup(r => r.DeviceGetByIdAsync(9)).ReturnsAsync(new Device { IDDevice = 9, TenantID = 1 });
        _repo.Setup(r => r.SimulationSessionDeviceAddAsync(5, 9)).ReturnsAsync(true);
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(1)).ReturnsAsync(new List<int> { 9 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.AddDeviceToSession(5, 9);

        Assert.IsType<OkResult>(result);
        // Strict mock: DeviceSimulationGetAsync/DeviceSimulationSetAsync have no setup, proving a virtual device never touches the physical-override table.
    }

    [Fact]
    public async Task StopSession_TurnsOffPhysicalMembersOnly_LeavesVirtualAlone()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10), ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            Devices = [new DeviceDto { IDDevice = 8 }, new DeviceDto { IDDevice = 9 }],
        });
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(1)).ReturnsAsync(new List<int> { 9 }); // 9 is virtual, 8 is physical
        _repo.Setup(r => r.DeviceSimulationSetAsync(8, It.Is<DeviceSimulation>(s => !s.Enabled))).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SimulationSessionStopAsync(5)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.StopSession(5);

        Assert.IsType<OkResult>(result);
        _repo.Verify(r => r.DeviceSimulationSetAsync(8, It.IsAny<DeviceSimulation>()), Times.Once);
        // Strict mock: DeviceSimulationSetAsync(9, ...) has no setup, proving the virtual member was never touched.
    }

    // ---- Simulation-scoped rules ----

    private static DeviceFarmUnitZoneRule ValidRelayRule() => new()
    {
        ActionType = ActionType.Relay,
        RelayFunction = RelayFunction.Heating,
        Name = "Test rule",
        TargetPercent = 100,
        Root = new ConditionNode { Type = NodeType.Comparison, Metric = SensorMetric.Temperature, Operator = ComparisonOperator.GreaterThan, Value1 = 30 },
    };

    [Fact]
    public async Task SessionRuleAdd_Valid_PinsScopeAndPersists()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        _repo.Setup(r => r.RulesGetForSimulationAsync(5)).ReturnsAsync(new List<DeviceFarmUnitZoneRule>());
        _repo.Setup(r => r.ServerConfigGetAsync(1)).ReturnsAsync(new ServerConfig());
        _repo.Setup(r => r.RuleAddAsync(It.Is<DeviceFarmUnitZoneRule>(rule =>
            rule.SimulationSessionID == 5 && rule.DeviceFarmUnitZoneID == null && rule.DeviceFarmUnitID == null && rule.DeviceFarmID == null && rule.TenantID == 1)))
            .ReturnsAsync(77);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionRuleAdd(5, ValidRelayRule());

        Assert.Equal(77, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task SessionRuleAdd_SessionAlreadyEnded_Returns400_NeverAdds()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-2), ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
        });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionRuleAdd(5, ValidRelayRule());

        Assert.IsType<BadRequestObjectResult>(result.Result);
        // MockBehavior.Strict: RuleAddAsync has no setup, proving the ended session was rejected before any shape check or write.
    }

    [Fact]
    public async Task SessionRuleAdd_ForeignTenantSession_Returns403_NeverAdds()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession { IDSimulationSession = 5, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionRuleAdd(5, ValidRelayRule());

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task SessionRuleAdd_InvalidShape_Returns400_NeverAdds()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionRuleAdd(5, new DeviceFarmUnitZoneRule { ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        // MockBehavior.Strict: RulesGetForSimulationAsync/RuleAddAsync have no setup, proving the shape error was caught before the cap check or the write.
    }

    [Fact]
    public async Task SessionRuleDelete_BelongsToSession_Deletes()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession { IDSimulationSession = 5, TenantID = 1 });
        _repo.Setup(r => r.RuleGetByIdAsync(77)).ReturnsAsync(new DeviceFarmUnitZoneRule { IDDeviceFarmUnitZoneRule = 77, TenantID = 1, SimulationSessionID = 5, ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "Test rule" });
        _repo.Setup(r => r.RulesReferencingAsync(77, 1)).ReturnsAsync(new List<DeviceFarmUnitZoneRule>());
        _repo.Setup(r => r.RuleDeleteAsync(77)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionRuleDelete(5, 77);

        Assert.True(result.Value);
    }

    [Fact]
    public async Task SessionRuleDelete_BelongsToDifferentSession_Returns404_NeverDeletes()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession { IDSimulationSession = 5, TenantID = 1 });
        _repo.Setup(r => r.RuleGetByIdAsync(77)).ReturnsAsync(new DeviceFarmUnitZoneRule { IDDeviceFarmUnitZoneRule = 77, TenantID = 1, SimulationSessionID = 6, ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "Other session's rule" });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionRuleDelete(5, 77);

        Assert.IsType<NotFoundResult>(result.Result);
        // MockBehavior.Strict: RuleDeleteAsync has no setup, proving a rule from a DIFFERENT session was never deleted just because both belong to the same caller's tenant.
    }

    // ---- Simulation groups ----

    [Fact]
    public async Task SessionGroupAdd_Valid_FansOutAndReturnsCreated()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        _repo.Setup(r => r.DeviceFarmUnitZoneGetByIdAsync(9)).ReturnsAsync(new DeviceFarmUnitZone { IDDeviceFarmUnitZone = 9, TenantID = 1 });
        var created = new SimulationGroup { IDSimulationGroup = 3, IDSimulationSession = 5, Scope = HierarchyNodeKind.Zone, ScopeID = 9, ScopeName = "Zone A", MemberDeviceCount = 2 };
        _repo.Setup(r => r.SimulationGroupAddAsync(It.Is<SimulationGroup>(g => g.IDSimulationSession == 5 && g.Scope == HierarchyNodeKind.Zone && g.ScopeID == 9)))
            .ReturnsAsync(created);
        _repo.Setup(r => r.AuditLogAddAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionGroupAdd(5, new SimulationGroup { Scope = HierarchyNodeKind.Zone, ScopeID = 9, Temperature = 30 });

        SimulationGroup body = Assert.IsType<SimulationGroup>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, body.MemberDeviceCount);
    }

    [Fact]
    public async Task SessionGroupAdd_ForeignTenantZone_Returns403_NeverAdds()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession
        {
            IDSimulationSession = 5, TenantID = 1, StartedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        });
        _repo.Setup(r => r.DeviceFarmUnitZoneGetByIdAsync(9)).ReturnsAsync(new DeviceFarmUnitZone { IDDeviceFarmUnitZone = 9, TenantID = 99 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionGroupAdd(5, new SimulationGroup { Scope = HierarchyNodeKind.Zone, ScopeID = 9 });

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        // MockBehavior.Strict: SimulationGroupAddAsync has no setup, proving a zone from a foreign tenant was rejected before any fan-out happened.
    }

    [Fact]
    public async Task SessionGroupDelete_BelongsToDifferentSession_Returns404_NeverDeletes()
    {
        _repo.Setup(r => r.SimulationSessionGetByIdAsync(5)).ReturnsAsync(new SimulationSession { IDSimulationSession = 5, TenantID = 1 });
        _repo.Setup(r => r.SimulationGroupGetByIdAsync(3)).ReturnsAsync(new SimulationGroup { IDSimulationGroup = 3, IDSimulationSession = 6, Scope = HierarchyNodeKind.Zone, ScopeID = 9 });
        var controller = NewController();
        SetCaller(controller, 1, "user", RoleNames.TenantAdmin);

        var result = await controller.SessionGroupDelete(5, 3);

        Assert.IsType<NotFoundResult>(result);
        // MockBehavior.Strict: SimulationGroupDeleteAsync has no setup, proving a group from a DIFFERENT session was never deleted.
    }
}
