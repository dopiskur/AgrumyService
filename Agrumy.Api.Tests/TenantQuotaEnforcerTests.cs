using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Moq;

namespace Agrumy.Api.Tests;

public class TenantQuotaEnforcerTests
{
    private readonly Mock<ITenantRepository> _tenantRepo = new(MockBehavior.Strict);
    private readonly Mock<IDeviceFarmUnitRepository> _deviceFarmUnitRepo = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<ISimulationRepository> _simulationRepo = new(MockBehavior.Strict);

    private TenantQuotaEnforcer NewEnforcer() =>
        new(_tenantRepo.Object, _deviceFarmUnitRepo.Object, _userRepo.Object, _simulationRepo.Object);

    [Fact]
    public async Task DefaultTenant_NeverConsultsQuota()
    {
        // IDTenant=0 is exempt without even reaching the repository - no .Setup() means a strict mock would throw if it were called.
        Assert.Null(await NewEnforcer().CheckCanAddFarmAsync(0));
        Assert.Null(await NewEnforcer().CheckCanAddUnitAsync(0));
        Assert.True(await NewEnforcer().IsMqttAllowedAsync(0));
        Assert.True(await NewEnforcer().IsGatewayAllowedAsync(0));
    }

    [Fact]
    public async Task NoQuotaRow_MeansUnlimited()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync((TenantQuota?)null);
        _deviceFarmUnitRepo.Setup(r => r.DeviceFarmsGetAsync(5)).ReturnsAsync(new List<DeviceFarm> { new(), new(), new() });

        Assert.Null(await NewEnforcer().CheckCanAddFarmAsync(5));
    }

    [Fact]
    public async Task AtFarmLimit_BlocksFurtherAdds()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5)); // MaxFarms = 1
        _deviceFarmUnitRepo.Setup(r => r.DeviceFarmsGetAsync(5)).ReturnsAsync(new List<DeviceFarm> { new() });

        string? result = await NewEnforcer().CheckCanAddFarmAsync(5);

        Assert.Equal(TenantQuotaEnforcer.LimitMessage, result);
    }

    [Fact]
    public async Task BelowFarmLimit_Allows()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5)); // MaxFarms = 1
        _deviceFarmUnitRepo.Setup(r => r.DeviceFarmsGetAsync(5)).ReturnsAsync(new List<DeviceFarm>());

        Assert.Null(await NewEnforcer().CheckCanAddFarmAsync(5));
    }

    [Fact]
    public async Task ZoneCount_SummedAcrossAllOfTenantsUnits()
    {
        var quota = TenantQuota.Default(5);
        quota.MaxUnits = 2; // widened so both units below are "allowed" to exist for this test
        quota.MaxZones = 3;
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(quota);
        _deviceFarmUnitRepo.Setup(r => r.DeviceFarmUnitsGetAsync(5)).ReturnsAsync(new List<DeviceFarmUnit>
        {
            new() { IDDeviceFarmUnit = 10 },
            new() { IDDeviceFarmUnit = 11 },
        });
        _deviceFarmUnitRepo.Setup(r => r.DeviceFarmUnitZonesGetAsync(10)).ReturnsAsync(new List<DeviceFarmUnitZone> { new(), new() });
        _deviceFarmUnitRepo.Setup(r => r.DeviceFarmUnitZonesGetAsync(11)).ReturnsAsync(new List<DeviceFarmUnitZone> { new() });

        // 2 + 1 = 3, at the limit - blocked.
        Assert.Equal(TenantQuotaEnforcer.LimitMessage, await NewEnforcer().CheckCanAddZoneAsync(5));
    }

    [Fact]
    public async Task ControllerRelayCount_OverLimit_Blocked()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5)); // MaxControllersPerDevice = 1

        Assert.Equal(TenantQuotaEnforcer.LimitMessage, await NewEnforcer().CheckControllerRelayCountAsync(5, relayCount: 2));
        Assert.Null(await NewEnforcer().CheckControllerRelayCountAsync(5, relayCount: 1));
    }

    [Fact]
    public async Task SensorFieldCount_OverLimit_Blocked()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5)); // MaxSensorsPerDevice = 3

        Assert.Equal(TenantQuotaEnforcer.LimitMessage, await NewEnforcer().CheckSensorFieldCountAsync(5, enabledSensorCount: 4));
        Assert.Null(await NewEnforcer().CheckSensorFieldCountAsync(5, enabledSensorCount: 3));
    }

    [Fact]
    public async Task MinSensorInterval_BelowMinimum_Blocked()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5)); // MinSensorIntervalMinutes = 5

        Assert.Equal(TenantQuotaEnforcer.LimitMessage, await NewEnforcer().CheckMinSensorIntervalAsync(5, sleepSeconds: 60));
        Assert.Null(await NewEnforcer().CheckMinSensorIntervalAsync(5, sleepSeconds: 300));
    }

    [Fact]
    public async Task MqttAndGateway_DefaultQuota_Disallowed()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5));

        Assert.False(await NewEnforcer().IsMqttAllowedAsync(5));
        Assert.False(await NewEnforcer().IsGatewayAllowedAsync(5));
    }

    [Fact]
    public async Task LoRa_DefaultQuota_Disallowed()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5));

        Assert.Equal(TenantQuotaEnforcer.LimitMessage, await NewEnforcer().CheckLoRaAllowedAsync(5));
    }

    [Fact]
    public async Task MaxUsers_AtLimit_Blocked()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5)); // MaxUsers = 1
        _userRepo.Setup(r => r.UsersGetAsync(5)).ReturnsAsync(new List<User> { new() });

        Assert.Equal(TenantQuotaEnforcer.LimitMessage, await NewEnforcer().CheckCanAddUserAsync(5));
    }

    [Fact]
    public async Task MaxSimulations_AtLimit_Blocked()
    {
        _tenantRepo.Setup(r => r.TenantQuotaGetAsync(5)).ReturnsAsync(TenantQuota.Default(5)); // MaxSimulations = 1
        _simulationRepo.Setup(r => r.SimulationSessionsGetAsync(5)).ReturnsAsync(new List<SimulationSession> { new() });

        Assert.Equal(TenantQuotaEnforcer.LimitMessage, await NewEnforcer().CheckCanAddSimulationAsync(5));
    }
}
