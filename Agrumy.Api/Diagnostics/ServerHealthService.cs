using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Agrumy.Api.Diagnostics
{
    public interface IServerHealthService
    {
        /// Only includes an entry for a dependency that is actually configured/enabled right now (roadmap #419) - e.g. no MQTT row when MqttTransportEnabled is false, so a deliberately-off integration never reads as a false alarm.
        Task<IReadOnlyList<ServerHealthEntry>> GetStatusesAsync(CancellationToken ct = default);
    }

    internal sealed class ServerHealthService(
        IServerConfigRepository serverConfigRepo,
        IConfiguration configuration,
        DatabaseHealthCheck databaseHealthCheck,
        CacheHealthCheck cacheHealthCheck,
        MqttHealthCheck mqttHealthCheck,
        EmailHealthCheck emailHealthCheck,
        FirmwareSourceHealthCheck firmwareSourceHealthCheck,
        WeatherHealthCheck weatherHealthCheck,
        GatewayHealthCheck gatewayHealthCheck,
        BackgroundWorkersHealthCheck backgroundWorkersHealthCheck) : IServerHealthService
    {
        private static readonly HealthCheckContext Context = new() { Registration = new HealthCheckRegistration("serverHealth", sp => null!, null, null) };

        public async Task<IReadOnlyList<ServerHealthEntry>> GetStatusesAsync(CancellationToken ct = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            var entries = new List<ServerHealthEntry>
            {
                await RunAsync("database", databaseHealthCheck, ct), // core dependency, always shown
                await RunAsync("backgroundWorkers", backgroundWorkersHealthCheck, ct), // always registered, regardless of which integrations are enabled
            };

            // Redis has no ServerConfig flag - "enabled" means an external backend is actually configured; an in-memory fallback has nothing external to check.
            if (!string.IsNullOrWhiteSpace(configuration["Cache:Redis:ConnectionString"]))
            {
                entries.Add(await RunAsync("redis", cacheHealthCheck, ct));
            }

            if (config.MqttTransportEnabled)
            {
                entries.Add(await RunAsync("mqtt", mqttHealthCheck, ct));
            }

            if (config.EmailEnabled)
            {
                entries.Add(await RunAsync("email", emailHealthCheck, ct));
            }

            // Local source never reaches the network - nothing external to check.
            if (config.FirmwareSource != FirmwareSource.Local)
            {
                entries.Add(await RunAsync("firmwareSource", firmwareSourceHealthCheck, ct));
            }

            // No explicit ServerConfig flag - WeatherEvaluator itself only runs once a location is set (see its own early-return).
            if (config.WeatherLocationLat.HasValue && config.WeatherLocationLon.HasValue)
            {
                entries.Add(await RunAsync("weather", weatherHealthCheck, ct));
            }

            if (config.GatewayEnabled)
            {
                entries.Add(await RunAsync("gateway", gatewayHealthCheck, ct));
            }

            return entries;
        }

        private static async Task<ServerHealthEntry> RunAsync(string name, IHealthCheck check, CancellationToken ct)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            HealthCheckResult result = await check.CheckHealthAsync(Context, ct);
            stopwatch.Stop();
            return new ServerHealthEntry(name, result.Status.ToString(), result.Description, Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2));
        }
    }
}
