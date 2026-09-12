using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Firmware;
using Agrumy.Api.Weather;

namespace Agrumy.Api.Startup
{
    /// Every periodic evaluator and its hosted service, plus the one-at-a-time background job runner.
    public static class BackgroundWorkerServiceExtensions
    {
        public static WebApplicationBuilder AddAgrumyBackgroundWorkers(this WebApplicationBuilder builder)
        {
            IServiceCollection services = builder.Services;

            // Scoped, not singleton: evaluators resolve scoped repositories/dispatcher themselves, and PeriodicBackgroundService creates a fresh DI scope per tick.
            services.AddScoped<OfflineAlertEvaluator>();
            services.AddHostedService<OfflineAlertBackgroundService>();

            services.AddScoped<LowBatteryAlertEvaluator>();
            services.AddHostedService<LowBatteryAlertBackgroundService>();

            services.AddScoped<TankRefillAlertEvaluator>();
            services.AddHostedService<TankRefillAlertBackgroundService>();

            // Server-side evaluator for Notification-action rules - a Relay-action rule's fold runs on-device instead.
            services.AddScoped<RuleNotificationEvaluator>();
            services.AddHostedService<RuleNotificationBackgroundService>();

            // MariaDB retention runs here; PostgreSQL/TimescaleDB installs use EfRepository.ApplyRetentionPolicyAsync (a native TimescaleDB policy) instead.
            services.AddScoped<SensorDataRetentionEvaluator>();
            services.AddHostedService<SensorDataRetentionBackgroundService>();

            // Opt-in (ServerConfig.PurgeOrphanedSensorDataScheduleEnabled) daily companion to the manual "Purge orphaned sensor data" trigger on DataMaintenanceApiController.
            services.AddScoped<PurgeOrphanedSensorDataEvaluator>();
            services.AddHostedService<PurgeOrphanedSensorDataBackgroundService>();

            // MariaDB/MySQL-only archive to a separate admin-configured database, no-op unless ServerConfig.ArchiveEnabled - moves rows instead of deleting them.
            services.AddScoped<SensorDataArchiveEvaluator>();
            services.AddHostedService<SensorDataArchiveBackgroundService>();

            // Hard 48h simulation-session safety cutoff.
            services.AddScoped<SimulationSessionExpiryEvaluator>();
            services.AddHostedService<SimulationSessionExpiryBackgroundService>();

            services.AddScoped<DeviceOutboxRetentionEvaluator>();
            services.AddHostedService<DeviceOutboxRetentionBackgroundService>();

            services.AddScoped<DeviceOutboxDispatchEvaluator>();
            services.AddHostedService<DeviceOutboxDispatchBackgroundService>();

            services.AddScoped<TenantUsageSnapshotEvaluator>();
            services.AddHostedService<TenantUsageSnapshotBackgroundService>();

            services.AddHttpClient<IWeatherForecastClient, OpenWeatherMapClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            services.AddScoped<WeatherEvaluator>();
            services.AddHostedService<WeatherBackgroundService>();

            services.AddScoped<FrostAlertEvaluator>();
            services.AddHostedService<FrostAlertBackgroundService>();

            services.AddScoped<SatelliteSyncEvaluator>();
            services.AddHostedService<SatelliteSyncBackgroundService>();
            services.AddScoped<SatelliteRasterRetentionEvaluator>();
            services.AddHostedService<SatelliteRasterRetentionBackgroundService>();

            services.AddHttpClient<Agrumy.Api.Arkod.ArkodGeoPackageSyncEvaluator>(client => client.Timeout = TimeSpan.FromMinutes(60));
            services.AddHostedService<ArkodGeoPackageSyncBackgroundService>();

            // Singleton so it outlives any one request's DI scope; BackgroundJobRunner consumes it one job at a time.
            services.AddSingleton<BackgroundJobQueue>();
            services.AddHostedService<BackgroundJobRunner>();

            services.AddScoped<FirmwareCatalogRefreshEvaluator>();
            services.AddHostedService<FirmwareCatalogRefreshBackgroundService>();

            // BaseAddress is the server's OWN bound address (the same "Urls" key Kestrel binds to), not the public WebView:ApiService URL - a virtual device's self-loopback traffic must not depend on the public hostname or count against the public rate limiters. Falls back to the documented local dev default if "Urls" is unset.
            services.AddHttpClient(VirtualDeviceRunnerBackgroundService.HttpClientName, (sp, client) =>
            {
                string urls = sp.GetRequiredService<IConfiguration>()["Urls"] ?? "http://localhost:5000";
                string firstUrl = urls.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "http://localhost:5000";
                client.BaseAddress = new Uri(firstUrl);
            });
            services.AddSingleton<Agrumy.Api.Simulation.SimulatedSensorGenerator>();
            services.AddHostedService<VirtualDeviceRunnerBackgroundService>();

            return builder;
        }
    }
}
