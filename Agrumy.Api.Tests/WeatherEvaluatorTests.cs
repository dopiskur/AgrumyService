using Agrumy.Shared;
using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Weather;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises WeatherEvaluator directly, no database or real HTTP call. Strict mocks double as the assertion that an early-return path makes NO further calls. Single-organization scenarios use organization 5 throughout, matching TenantsGetAllAsync's own mock, with no Farms/Units/Parcels of its own - so TenantWeatherLocations.ResolveDistinctAsync always resolves to exactly the tenant's own default location.
public class WeatherEvaluatorTests
{
    private readonly Mock<IServerConfigRepository> _serverConfig = new(MockBehavior.Strict);
    private readonly Mock<ITenantRepository> _tenants = new(MockBehavior.Strict);
    private readonly Mock<IDeviceFarmUnitRepository> _units = new(MockBehavior.Strict);
    private readonly Mock<IFarmParcelRepository> _farmParcels = new(MockBehavior.Strict);
    private readonly Mock<ISimulationRepository> _simulation = new(MockBehavior.Strict);
    private readonly Mock<IWeatherForecastClient> _weatherClient = new(MockBehavior.Strict);

    private const int TenantId = 5;
    private const double Lat = 45.8;
    private const double Lon = 16.0;

    private WeatherEvaluator NewEvaluator(string? apiKey = "test-key") =>
        new(_serverConfig.Object, _tenants.Object, _units.Object, _farmParcels.Object, _simulation.Object, _weatherClient.Object,
            Options.Create(new AgrumySettings { WeatherApiKey = apiKey, WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 }),
            NullLogger<WeatherEvaluator>.Instance);

    private void SetupServerConfig(ServerConfig config) =>
        _serverConfig.Setup(s => s.ServerConfigGetAsync(1)).ReturnsAsync(config);

    private void SetupTenant(double? lat, double? lon)
    {
        _tenants.Setup(t => t.TenantsGetAllAsync()).ReturnsAsync(new List<Tenant> { new() { IDTenant = TenantId, Latitude = lat, Longitude = lon } });
        // No Farms/Units/Parcels/weather-feed devices of its own - TenantWeatherLocations.ResolveDistinctAsync always walks these regardless of whether a location ends up resolvable.
        _units.Setup(u => u.DeviceFarmsGetAsync(TenantId)).ReturnsAsync(Array.Empty<DeviceFarm>());
        _units.Setup(u => u.DeviceFarmUnitsGetAsync(TenantId)).ReturnsAsync(Array.Empty<DeviceFarmUnit>());
        _farmParcels.Setup(f => f.FarmParcelZonesWithGeometryGetAsync(TenantId)).ReturnsAsync(Array.Empty<FarmParcelZone>());
        _simulation.Setup(s => s.ActiveWeatherFeedDeviceLocationsGetAsync(TenantId)).ReturnsAsync(Array.Empty<(double, double)>());
    }

    private void SetupState(DateTimeOffset? checkedAtUtc) =>
        _tenants.Setup(t => t.WeatherLocationStateGetAsync(TenantId, Lat, Lon)).ReturnsAsync(new WeatherLocationState { TenantID = TenantId, Latitude = Lat, Longitude = Lon, WeatherCheckedAtUtc = checkedAtUtc });

    [Fact]
    public async Task No_ApiKey_Configured_Does_Nothing()
    {
        SetupServerConfig(new ServerConfig());
        await NewEvaluator(apiKey: null).RunOnceAsync();
        // Strict mocks: TenantsGetAllAsync/GetMaxRainProbabilityPercentAsync would throw if called.
    }

    [Theory]
    [InlineData(null, 15.0)]
    [InlineData(45.5, null)]
    public async Task Location_Not_Fully_Set_Does_Nothing(double? lat, double? lon)
    {
        SetupServerConfig(new ServerConfig());
        SetupTenant(lat, lon);

        await NewEvaluator().RunOnceAsync();

        // No resolvable location (tenant default incomplete, no Farms/Units/Parcels) -> ResolveDistinctAsync returns empty, so WeatherLocationStateGetAsync/GetMaxRainProbabilityPercentAsync would throw if called.
    }

    [Fact]
    public async Task Not_Due_Yet_Skips_The_Fetch()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15 });
        SetupTenant(Lat, Lon);
        SetupState(DateTimeOffset.UtcNow.AddMinutes(-5)); // < 15 minutes ago

        await NewEvaluator().RunOnceAsync();

        // Strict mock: GetMaxRainProbabilityPercentAsync would throw if called.
    }

    [Fact]
    public async Task Never_Checked_Before_Is_Due_Immediately()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(Lat, Lon);
        SetupState(null);
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10.0);
        _tenants.Setup(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, false, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _weatherClient.Setup(c => c.GetCurrentOutdoorConditionsAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutdoorConditions?)null);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, false, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Rain_Probability_At_Or_Above_Threshold_Sets_RainPredicted_True()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(Lat, Lon);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(50.0); // exactly at threshold - counts as predicted, same >= convention as LowBatteryAlertEvaluator's threshold check
        _tenants.Setup(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, true, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _weatherClient.Setup(c => c.GetCurrentOutdoorConditionsAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutdoorConditions?)null);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, true, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Rain_Probability_Below_Threshold_Sets_RainPredicted_False()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(Lat, Lon);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(49.9);
        _tenants.Setup(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, false, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _weatherClient.Setup(c => c.GetCurrentOutdoorConditionsAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutdoorConditions?)null);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, false, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Outdoor_Conditions_Fetched_And_Persisted_Alongside_Rain_Check()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(Lat, Lon);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10.0);
        _tenants.Setup(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, false, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _weatherClient.Setup(c => c.GetCurrentOutdoorConditionsAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutdoorConditions(22.5, 61.0, 3.2, 1013.0));
        _tenants.Setup(t => t.WeatherLocationStateSetOutdoorAsync(TenantId, Lat, Lon, 22.5, 61.0, 3.2, 1013.0, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.WeatherLocationStateSetOutdoorAsync(TenantId, Lat, Lon, 22.5, 61.0, 3.2, 1013.0, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Outdoor_Fetch_Failure_Leaves_Last_Known_Outdoor_State_Untouched()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(Lat, Lon);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10.0);
        _tenants.Setup(t => t.WeatherLocationStateSetWeatherAsync(TenantId, Lat, Lon, false, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _weatherClient.Setup(c => c.GetCurrentOutdoorConditionsAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutdoorConditions?)null);

        await NewEvaluator().RunOnceAsync();

        // Strict mock: WeatherLocationStateSetOutdoorAsync would throw if called - a failed fetch must not overwrite the last good reading.
        _tenants.Verify(t => t.WeatherLocationStateSetOutdoorAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task Failed_Fetch_Leaves_Last_Known_State_Untouched()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15 });
        SetupTenant(Lat, Lon);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(Lat, Lon, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        await NewEvaluator().RunOnceAsync();

        // Strict mock: WeatherLocationStateSetWeatherAsync would throw if called - a failed fetch must not overwrite the last good reading.
    }

    [Fact]
    public async Task Missing_ServerConfig_PollInterval_Falls_Back_To_AgrumySettings_Default()
    {
        // A row with WeatherPollIntervalMinutes == null must fall back to AgrumySettings.WeatherPollIntervalMinutes, not treat null as "always due" or throw.
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = null });
        SetupTenant(Lat, Lon);
        SetupState(DateTimeOffset.UtcNow.AddMinutes(-5)); // < 15 minutes ago -> not due

        await NewEvaluator().RunOnceAsync();

        // Strict mock: GetMaxRainProbabilityPercentAsync would throw if called.
    }
}
