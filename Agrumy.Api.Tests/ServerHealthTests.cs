using System.Net;
using System.Net.Sockets;
using Agrumy.Api.Commands;
using Agrumy.Api.Dal;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Diagnostics;
using Agrumy.Api.Firmware;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace Agrumy.Api.Tests;

/// Roadmap #419 - Server Health card's per-dependency checks, plus ServerHealthService's "only list a currently-enabled dependency" filtering.
public class ServerHealthTests
{
    private static readonly HealthCheckContext Context = new() { Registration = new HealthCheckRegistration("test", sp => null!, null, null) };

    private sealed class FakeServerConfigRepository(ServerConfig config) : IServerConfigRepository
    {
        public Task<ServerConfig> ServerConfigGetAsync(int idServerConfig) => Task.FromResult(config);
        public Task ServerConfigUpdateAsync(ServerConfig c) => Task.CompletedTask;
        public Task ServerConfigReloadFromAppSettingsAsync(int idServerConfig) => Task.CompletedTask;
        public Task ServerConfigWeatherStateSetAsync(bool rainPredicted, DateTimeOffset checkedAtUtc, int idServerConfig) => Task.CompletedTask;
        public Task ServerConfigFirmwareRefreshStateSetAsync(DateTimeOffset checkedAtUtc, int idServerConfig) => Task.CompletedTask;
        public Task ServerConfigArchiveRunStateSetAsync(DateTimeOffset ranAtUtc, int idServerConfig) => Task.CompletedTask;
        public Task ApplyRetentionPolicyAsync(int? retentionDays) => Task.CompletedTask;
    }

    private sealed class FakeConfiguration(string? redisConnectionString = null) : IConfiguration
    {
        public string? this[string key]
        {
            get => key == "Cache:Redis:ConnectionString" ? redisConnectionString : null;
            set => throw new NotImplementedException();
        }
        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private sealed class FakeMqttConnectionManager(bool connects, Exception? throwOnConnect = null) : IMqttConnectionManager
    {
        public Task PublishAsync(string brokerHost, int brokerPort, string? username, string? password, MQTTnet.MqttApplicationMessage message, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<bool> TestConnectionAsync(string brokerHost, int brokerPort, string? username, string? password, CancellationToken ct = default) =>
            throwOnConnect is not null ? throw throwOnConnect : Task.FromResult(connects);
    }

    private sealed class FakeFirmwareFetcher(string? response, Exception? throwOnFetch = null) : IFirmwareFetcher
    {
        public string? LastUrl { get; private set; }

        public Task<string> GetStringAsync(string url, bool gitHubApi, CancellationToken cancellationToken = default)
        {
            LastUrl = url;
            return throwOnFetch is not null ? throw throwOnFetch : Task.FromResult(response ?? "");
        }

        public Task<Stream> GetStreamAsync(string url, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    // ---- MqttHealthCheck ----------------------------------------------------

    [Fact]
    public async Task MqttHealthCheck_NoBrokerHost_ReturnsDegraded()
    {
        var check = new MqttHealthCheck(new FakeServerConfigRepository(new ServerConfig()), new FakeMqttConnectionManager(true));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task MqttHealthCheck_Connects_ReturnsHealthy()
    {
        var config = new ServerConfig { MqttBrokerHost = "broker.local", MqttBrokerPort = 1883 };
        var check = new MqttHealthCheck(new FakeServerConfigRepository(config), new FakeMqttConnectionManager(true));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task MqttHealthCheck_DoesNotConnect_ReturnsUnhealthy()
    {
        var config = new ServerConfig { MqttBrokerHost = "broker.local", MqttBrokerPort = 1883 };
        var check = new MqttHealthCheck(new FakeServerConfigRepository(config), new FakeMqttConnectionManager(false));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task MqttHealthCheck_Throws_ReturnsUnhealthyNotPropagating()
    {
        var config = new ServerConfig { MqttBrokerHost = "broker.local", MqttBrokerPort = 1883 };
        var check = new MqttHealthCheck(new FakeServerConfigRepository(config), new FakeMqttConnectionManager(false, new InvalidOperationException("refused")));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    // ---- EmailHealthCheck -----------------------------------------------------

    [Fact]
    public async Task EmailHealthCheck_NoHost_ReturnsDegraded()
    {
        var check = new EmailHealthCheck(new FakeServerConfigRepository(new ServerConfig()));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task EmailHealthCheck_PortReachable_ReturnsHealthy()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var config = new ServerConfig { EmailHost = "127.0.0.1", EmailPort = port };
        var check = new EmailHealthCheck(new FakeServerConfigRepository(config));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        listener.Stop();
    }

    [Fact]
    public async Task EmailHealthCheck_PortUnreachable_ReturnsUnhealthy()
    {
        // Port 1 on loopback is reserved (TCP port scanners' classic "always closed") - refuses immediately, no timeout wait.
        var config = new ServerConfig { EmailHost = "127.0.0.1", EmailPort = 1 };
        var check = new EmailHealthCheck(new FakeServerConfigRepository(config));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    // ---- FirmwareSourceHealthCheck ---------------------------------------------

    [Fact]
    public async Task FirmwareSourceHealthCheck_Local_ReturnsHealthyWithoutFetching()
    {
        var config = new ServerConfig { FirmwareSource = FirmwareSource.Local };
        var fetcher = new FakeFirmwareFetcher(null, new InvalidOperationException("should not be called"));
        var check = new FirmwareSourceHealthCheck(new FakeServerConfigRepository(config), fetcher);

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task FirmwareSourceHealthCheck_GitHubNoRepository_ReturnsDegraded()
    {
        var config = new ServerConfig { FirmwareSource = FirmwareSource.GitHub };
        var check = new FirmwareSourceHealthCheck(new FakeServerConfigRepository(config), new FakeFirmwareFetcher(null));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task FirmwareSourceHealthCheck_GitHubReachable_ReturnsHealthy()
    {
        var config = new ServerConfig { FirmwareSource = FirmwareSource.GitHub, FirmwareGitHubRepository = "owner/repo" };
        var fetcher = new FakeFirmwareFetcher("{}");
        var check = new FirmwareSourceHealthCheck(new FakeServerConfigRepository(config), fetcher);

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("https://api.github.com/repos/owner/repo", fetcher.LastUrl);
    }

    [Fact]
    public async Task FirmwareSourceHealthCheck_CustomUnreachable_ReturnsUnhealthy()
    {
        var config = new ServerConfig { FirmwareSource = FirmwareSource.Custom, FirmwareCustomRepositoryUrl = "https://example.com/manifest.json" };
        var fetcher = new FakeFirmwareFetcher(null, new HttpRequestException("DNS failure"));
        var check = new FirmwareSourceHealthCheck(new FakeServerConfigRepository(config), fetcher);

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    // ---- WeatherHealthCheck ------------------------------------------------

    [Fact]
    public async Task WeatherHealthCheck_NeverChecked_ReturnsDegraded()
    {
        var check = new WeatherHealthCheck(new FakeServerConfigRepository(new ServerConfig()));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task WeatherHealthCheck_RecentlyChecked_ReturnsHealthy()
    {
        var config = new ServerConfig { WeatherCheckedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5), WeatherPollIntervalMinutes = 30 };
        var check = new WeatherHealthCheck(new FakeServerConfigRepository(config));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task WeatherHealthCheck_Stale_ReturnsDegraded()
    {
        var config = new ServerConfig { WeatherCheckedAtUtc = DateTimeOffset.UtcNow.AddHours(-5), WeatherPollIntervalMinutes = 30 };
        var check = new WeatherHealthCheck(new FakeServerConfigRepository(config));

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    // ---- GatewayHealthCheck ------------------------------------------------

    [Fact]
    public async Task GatewayHealthCheck_NoGatewaysRegistered_ReturnsDegraded()
    {
        var gatewayRepo = new Mock<IGatewayRepository>();
        gatewayRepo.Setup(r => r.GatewayDevicesGetAllAsync()).ReturnsAsync(new List<Device>());
        var deviceRepo = new Mock<IDeviceRepository>();
        var check = new GatewayHealthCheck(gatewayRepo.Object, deviceRepo.Object);

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task GatewayHealthCheck_RegisteredGatewayOnline_ReturnsHealthy()
    {
        var gatewayRepo = new Mock<IGatewayRepository>();
        gatewayRepo.Setup(r => r.GatewayDevicesGetAllAsync()).ReturnsAsync(new List<Device> { new() { IDDevice = 7 } });
        var deviceRepo = new Mock<IDeviceRepository>();
        deviceRepo.Setup(r => r.DeviceFleetGetAsync(null)).ReturnsAsync(new List<DeviceFleetStatus> { new() { IDDevice = 7, Online = true } });
        var check = new GatewayHealthCheck(gatewayRepo.Object, deviceRepo.Object);

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task GatewayHealthCheck_RegisteredGatewayOffline_ReturnsUnhealthy()
    {
        var gatewayRepo = new Mock<IGatewayRepository>();
        gatewayRepo.Setup(r => r.GatewayDevicesGetAllAsync()).ReturnsAsync(new List<Device> { new() { IDDevice = 7 } });
        var deviceRepo = new Mock<IDeviceRepository>();
        deviceRepo.Setup(r => r.DeviceFleetGetAsync(null)).ReturnsAsync(new List<DeviceFleetStatus> { new() { IDDevice = 7, Online = false } });
        var check = new GatewayHealthCheck(gatewayRepo.Object, deviceRepo.Object);

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    // ---- ServerHealthService: only lists currently-enabled dependencies -------

    private static ServerHealthService BuildService(ServerConfig config, IConfiguration? configuration = null)
    {
        var repo = new FakeServerConfigRepository(config);
        configuration ??= new FakeConfiguration();
        var gatewayRepo = new Mock<IGatewayRepository>();
        gatewayRepo.Setup(r => r.GatewayDevicesGetAllAsync()).ReturnsAsync(new List<Device>());
        var deviceRepo = new Mock<IDeviceRepository>();
        deviceRepo.Setup(r => r.DeviceFleetGetAsync(null)).ReturnsAsync(new List<DeviceFleetStatus>());

        return new ServerHealthService(
            repo, configuration,
            new DatabaseHealthCheck(new FakeSystemRepositoryAlwaysOk()),
            new CacheHealthCheck(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
                Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()))),
            new MqttHealthCheck(repo, new FakeMqttConnectionManager(true)),
            new EmailHealthCheck(repo),
            new FirmwareSourceHealthCheck(repo, new FakeFirmwareFetcher("{}")),
            new WeatherHealthCheck(repo),
            new GatewayHealthCheck(gatewayRepo.Object, deviceRepo.Object));
    }

    private sealed class FakeSystemRepositoryAlwaysOk : ISystemRepository
    {
        public Task<bool> TestConnectionAsync() => Task.FromResult(true);
        public Task EnsureSchemaAsync() => Task.CompletedTask;
        public DbFailureKind ClassifyException(Exception ex) => DbFailureKind.Unknown;
    }

    [Fact]
    public async Task ServerHealthService_EverythingDisabled_OnlyListsDatabase()
    {
        var service = BuildService(new ServerConfig());

        var entries = await service.GetStatusesAsync();

        Assert.Single(entries, e => e.Name == "database");
    }

    [Fact]
    public async Task ServerHealthService_MqttEnabled_IncludesMqttEntry()
    {
        var config = new ServerConfig { MqttTransportEnabled = true, MqttBrokerHost = "broker.local" };
        var service = BuildService(config);

        var entries = await service.GetStatusesAsync();

        Assert.Contains(entries, e => e.Name == "mqtt");
    }

    [Fact]
    public async Task ServerHealthService_MqttDisabled_ExcludesMqttEntry()
    {
        var config = new ServerConfig { MqttTransportEnabled = false, MqttBrokerHost = "broker.local" };
        var service = BuildService(config);

        var entries = await service.GetStatusesAsync();

        Assert.DoesNotContain(entries, e => e.Name == "mqtt");
    }

    [Fact]
    public async Task ServerHealthService_RedisConfigured_IncludesRedisEntry()
    {
        var service = BuildService(new ServerConfig(), new FakeConfiguration("localhost:6379"));

        var entries = await service.GetStatusesAsync();

        Assert.Contains(entries, e => e.Name == "redis");
    }

    [Fact]
    public async Task ServerHealthService_RedisNotConfigured_ExcludesRedisEntry()
    {
        var service = BuildService(new ServerConfig());

        var entries = await service.GetStatusesAsync();

        Assert.DoesNotContain(entries, e => e.Name == "redis");
    }

    [Fact]
    public async Task ServerHealthService_FirmwareSourceLocal_ExcludesFirmwareEntry()
    {
        var service = BuildService(new ServerConfig { FirmwareSource = FirmwareSource.Local });

        var entries = await service.GetStatusesAsync();

        Assert.DoesNotContain(entries, e => e.Name == "firmwareSource");
    }

    [Fact]
    public async Task ServerHealthService_WeatherLocationSet_IncludesWeatherEntry()
    {
        var config = new ServerConfig { WeatherLocationLat = 45.8, WeatherLocationLon = 15.9 };
        var service = BuildService(config);

        var entries = await service.GetStatusesAsync();

        Assert.Contains(entries, e => e.Name == "weather");
    }

    [Fact]
    public async Task ServerHealthService_GatewayEnabled_IncludesGatewayEntry()
    {
        var config = new ServerConfig { GatewayEnabled = true };
        var service = BuildService(config);

        var entries = await service.GetStatusesAsync();

        Assert.Contains(entries, e => e.Name == "gateway");
    }
}
