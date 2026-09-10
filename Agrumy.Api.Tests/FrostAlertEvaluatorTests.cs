using Agrumy.Shared;
using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Weather;
using Agrumy.Api.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises FrostAlertEvaluator directly, no database or real HTTP call. Strict mocks double as the assertion that an early-return path makes NO further calls.
public class FrostAlertEvaluatorTests
{
    private readonly Mock<IServerConfigRepository> _serverConfig = new(MockBehavior.Strict);
    private readonly Mock<IDeviceRepository> _devices = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<IWeatherForecastClient> _weatherClient = new(MockBehavior.Strict);
    private readonly Mock<INotificationDispatcher> _dispatcher = new(MockBehavior.Strict);

    private FrostAlertEvaluator NewEvaluator(string? apiKey = "test-key") =>
        new(_serverConfig.Object, _devices.Object, _users.Object, _weatherClient.Object, _dispatcher.Object,
            Options.Create(new AgrumySettings
            {
                WeatherApiKey = apiKey,
                WeatherPollIntervalMinutes = 15,
                FrostLookaheadHours = 12,
                FrostTempThresholdC = 3.0,
                FrostCloudinessMaxPercent = 30.0,
                FrostWindMaxMetersPerSecond = 2.0,
            }),
            NullLogger<FrostAlertEvaluator>.Instance);

    private void SetupServerConfig(ServerConfig config) =>
        _serverConfig.Setup(s => s.ServerConfigGetAsync(1)).ReturnsAsync(config);

    private static ServerConfig BaseConfig(bool frostPredicted = false, DateTimeOffset? checkedAtUtc = null) => new()
    {
        WeatherLocationLat = 45.8,
        WeatherLocationLon = 16.0,
        WeatherPollIntervalMinutes = 15,
        FrostLookaheadHours = 12,
        FrostTempThresholdC = 3.0,
        FrostCloudinessMaxPercent = 30.0,
        FrostWindMaxMetersPerSecond = 2.0,
        FrostPredicted = frostPredicted,
        FrostCheckedAtUtc = checkedAtUtc,
    };

    [Fact]
    public async Task No_ApiKey_Configured_Does_Nothing()
    {
        await NewEvaluator(apiKey: null).RunOnceAsync();
        // Strict mocks: every dependency call would throw if reached.
    }

    [Theory]
    [InlineData(null, 15.0)]
    [InlineData(45.5, null)]
    public async Task Location_Not_Fully_Set_Does_Nothing(double? lat, double? lon)
    {
        SetupServerConfig(new ServerConfig { WeatherLocationLat = lat, WeatherLocationLon = lon });

        await NewEvaluator().RunOnceAsync();

        // Strict mock: GetFrostForecastAsync would throw if called.
    }

    [Fact]
    public async Task Not_Due_Yet_Skips_The_Fetch()
    {
        SetupServerConfig(BaseConfig(checkedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5))); // < 15 minutes ago

        await NewEvaluator().RunOnceAsync();

        // Strict mock: GetFrostForecastAsync would throw if called.
    }

    [Fact]
    public async Task Cold_Clear_Calm_Forecast_Predicts_Frost_And_Alerts_Once()
    {
        SetupServerConfig(BaseConfig(frostPredicted: false, checkedAtUtc: DateTimeOffset.UtcNow.AddHours(-1)));
        _weatherClient.Setup(c => c.GetFrostForecastAsync(45.8, 16.0, "test-key", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrostForecastResult(1.0, 10.0, 0.5, 6));
        _serverConfig.Setup(s => s.ServerConfigFrostStateSetAsync(true, 6, It.IsAny<DateTimeOffset>(), 1)).Returns(Task.CompletedTask);
        _devices.Setup(d => d.FrostSensorReadingsGetAsync()).ReturnsAsync(Array.Empty<FrostSensorReading>());
        _users.Setup(u => u.TenantAdminsGetAsync(0)).ReturnsAsync(Array.Empty<User>());

        await NewEvaluator().RunOnceAsync();

        _serverConfig.Verify(s => s.ServerConfigFrostStateSetAsync(true, 6, It.IsAny<DateTimeOffset>(), 1), Times.Once);
        // No recipients (empty admin list) -> NotificationRecipientBuilder short-circuits before DispatchToRecipientsAsync, so the strict dispatcher mock is never called.
    }

    [Fact]
    public async Task Already_Predicted_Does_Not_Re_Alert()
    {
        SetupServerConfig(BaseConfig(frostPredicted: true, checkedAtUtc: DateTimeOffset.UtcNow.AddHours(-1)));
        _weatherClient.Setup(c => c.GetFrostForecastAsync(45.8, 16.0, "test-key", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrostForecastResult(1.0, 10.0, 0.5, 6));
        _serverConfig.Setup(s => s.ServerConfigFrostStateSetAsync(true, 6, It.IsAny<DateTimeOffset>(), 1)).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        // Strict mocks: FrostSensorReadingsGetAsync/TenantAdminsGetAsync/DispatchToRecipientsAsync would throw if called - this is a continuing episode, already alerted.
    }

    [Theory]
    [InlineData(5.0, 10.0, 0.5)] // too warm
    [InlineData(1.0, 80.0, 0.5)] // too cloudy
    [InlineData(1.0, 10.0, 5.0)] // too windy
    public async Task Any_Factor_Outside_Threshold_Does_Not_Predict_Frost(double tempC, double cloudPercent, double windMps)
    {
        SetupServerConfig(BaseConfig(frostPredicted: false, checkedAtUtc: DateTimeOffset.UtcNow.AddHours(-1)));
        _weatherClient.Setup(c => c.GetFrostForecastAsync(45.8, 16.0, "test-key", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrostForecastResult(tempC, cloudPercent, windMps, 6));
        _serverConfig.Setup(s => s.ServerConfigFrostStateSetAsync(false, null, It.IsAny<DateTimeOffset>(), 1)).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _serverConfig.Verify(s => s.ServerConfigFrostStateSetAsync(false, null, It.IsAny<DateTimeOffset>(), 1), Times.Once);
        // Strict mocks: FrostSensorReadingsGetAsync/TenantAdminsGetAsync/DispatchToRecipientsAsync would throw if called - no alert on a no-risk tick.
    }

    [Fact]
    public async Task Failed_Fetch_Leaves_Last_Known_State_Untouched()
    {
        SetupServerConfig(BaseConfig(checkedAtUtc: DateTimeOffset.UtcNow.AddHours(-1)));
        _weatherClient.Setup(c => c.GetFrostForecastAsync(45.8, 16.0, "test-key", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FrostForecastResult?)null);

        await NewEvaluator().RunOnceAsync();

        // Strict mock: ServerConfigFrostStateSetAsync would throw if called - a failed fetch must not overwrite the last good reading.
    }

    [Fact]
    public async Task Narrow_Local_DewPoint_Spread_Is_Appended_To_The_Alert_Body()
    {
        SetupServerConfig(BaseConfig(frostPredicted: false, checkedAtUtc: DateTimeOffset.UtcNow.AddHours(-1)));
        _weatherClient.Setup(c => c.GetFrostForecastAsync(45.8, 16.0, "test-key", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrostForecastResult(1.0, 10.0, 0.5, 6));
        _serverConfig.Setup(s => s.ServerConfigFrostStateSetAsync(true, 6, It.IsAny<DateTimeOffset>(), 1)).Returns(Task.CompletedTask);
        // Temperature==Humidity-derived dew point close to Temperature itself -> a small spread, i.e. "local confirmation".
        _devices.Setup(d => d.FrostSensorReadingsGetAsync()).ReturnsAsync(new[] { new FrostSensorReading(1.5, 98.0) });
        var admin = new User { IDUser = 1, Email = "admin@agrumy.local" };
        _users.Setup(u => u.TenantAdminsGetAsync(0)).ReturnsAsync(new[] { admin });
        _users.Setup(u => u.NotificationDisabledChannelsGetAsync(It.IsAny<IEnumerable<int>>(), NotificationEventType.Frost))
            .ReturnsAsync(new Dictionary<int, HashSet<string>>());
        _dispatcher.Setup(d => d.DispatchToRecipientsAsync(
                "Agrumy: frost predicted",
                It.Is<string>(body => body.Contains("narrowing dew-point spread")),
                NotificationSeverity.Warning,
                It.IsAny<IReadOnlyList<NotificationRecipient>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChannelOutcome>());

        await NewEvaluator().RunOnceAsync();

        _dispatcher.VerifyAll();
    }
}
