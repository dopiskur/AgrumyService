using Agrumy.Shared;
using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Weather;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises WeatherEvaluator directly, no database or real HTTP call. Strict mocks double as the assertion that an early-return path makes NO further calls. Single-tenant scenarios use tenant 5 throughout, matching TenantsGetAllAsync's own mock.
public class WeatherEvaluatorTests
{
    private readonly Mock<IServerConfigRepository> _serverConfig = new(MockBehavior.Strict);
    private readonly Mock<ITenantRepository> _tenants = new(MockBehavior.Strict);
    private readonly Mock<IWeatherForecastClient> _weatherClient = new(MockBehavior.Strict);

    private const int TenantId = 5;

    private WeatherEvaluator NewEvaluator(string? apiKey = "test-key") =>
        new(_serverConfig.Object, _tenants.Object, _weatherClient.Object,
            Options.Create(new AgrumySettings { WeatherApiKey = apiKey, WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 }),
            NullLogger<WeatherEvaluator>.Instance);

    private void SetupServerConfig(ServerConfig config) =>
        _serverConfig.Setup(s => s.ServerConfigGetAsync(1)).ReturnsAsync(config);

    private void SetupTenant(double? lat, double? lon) =>
        _tenants.Setup(t => t.TenantsGetAllAsync()).ReturnsAsync(new List<Tenant> { new() { IDTenant = TenantId, Latitude = lat, Longitude = lon } });

    private void SetupState(DateTimeOffset? checkedAtUtc) =>
        _tenants.Setup(t => t.TenantWeatherStateGetAsync(TenantId)).ReturnsAsync(new TenantWeatherState { TenantID = TenantId, WeatherCheckedAtUtc = checkedAtUtc });

    [Fact]
    public async Task No_ApiKey_Configured_Does_Nothing()
    {
        await NewEvaluator(apiKey: null).RunOnceAsync();
        // Strict mocks: ServerConfigGetAsync/TenantsGetAllAsync/GetMaxRainProbabilityPercentAsync would throw if called.
    }

    [Theory]
    [InlineData(null, 15.0)]
    [InlineData(45.5, null)]
    public async Task Location_Not_Fully_Set_Does_Nothing(double? lat, double? lon)
    {
        SetupServerConfig(new ServerConfig());
        SetupTenant(lat, lon);

        await NewEvaluator().RunOnceAsync();

        // Strict mock: TenantWeatherStateGetAsync/GetMaxRainProbabilityPercentAsync would throw if called.
    }

    [Fact]
    public async Task Not_Due_Yet_Skips_The_Fetch()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15 });
        SetupTenant(45.8, 16.0);
        SetupState(DateTimeOffset.UtcNow.AddMinutes(-5)); // < 15 minutes ago

        await NewEvaluator().RunOnceAsync();

        // Strict mock: GetMaxRainProbabilityPercentAsync would throw if called.
    }

    [Fact]
    public async Task Never_Checked_Before_Is_Due_Immediately()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(45.8, 16.0);
        SetupState(null);
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(45.8, 16.0, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10.0);
        _tenants.Setup(t => t.TenantWeatherStateSetWeatherAsync(TenantId, false, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.TenantWeatherStateSetWeatherAsync(TenantId, false, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Rain_Probability_At_Or_Above_Threshold_Sets_RainPredicted_True()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(45.8, 16.0);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(45.8, 16.0, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(50.0); // exactly at threshold - counts as predicted, same >= convention as LowBatteryAlertEvaluator's threshold check
        _tenants.Setup(t => t.TenantWeatherStateSetWeatherAsync(TenantId, true, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.TenantWeatherStateSetWeatherAsync(TenantId, true, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Rain_Probability_Below_Threshold_Sets_RainPredicted_False()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15, WeatherRainSkipThreshold = 50.0 });
        SetupTenant(45.8, 16.0);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(45.8, 16.0, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(49.9);
        _tenants.Setup(t => t.TenantWeatherStateSetWeatherAsync(TenantId, false, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _tenants.Verify(t => t.TenantWeatherStateSetWeatherAsync(TenantId, false, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Failed_Fetch_Leaves_Last_Known_State_Untouched()
    {
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = 15 });
        SetupTenant(45.8, 16.0);
        SetupState(DateTimeOffset.UtcNow.AddHours(-1));
        _weatherClient.Setup(c => c.GetMaxRainProbabilityPercentAsync(45.8, 16.0, "test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        await NewEvaluator().RunOnceAsync();

        // Strict mock: TenantWeatherStateSetWeatherAsync would throw if called - a failed fetch must not overwrite the last good reading.
    }

    [Fact]
    public async Task Missing_ServerConfig_PollInterval_Falls_Back_To_AgrumySettings_Default()
    {
        // A row with WeatherPollIntervalMinutes == null must fall back to AgrumySettings.WeatherPollIntervalMinutes, not treat null as "always due" or throw.
        SetupServerConfig(new ServerConfig { WeatherPollIntervalMinutes = null });
        SetupTenant(45.8, 16.0);
        SetupState(DateTimeOffset.UtcNow.AddMinutes(-5)); // < 15 minutes ago -> not due

        await NewEvaluator().RunOnceAsync();

        // Strict mock: GetMaxRainProbabilityPercentAsync would throw if called.
    }
}
