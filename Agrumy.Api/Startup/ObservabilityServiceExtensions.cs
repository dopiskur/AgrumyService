using Agrumy.Api.Diagnostics;
using OpenTelemetry.Metrics;

namespace Agrumy.Api.Startup
{
    /// Metrics, health checks, the Prometheus exporter, and console logging.
    public static class ObservabilityServiceExtensions
    {
        public static WebApplicationBuilder AddAgrumyObservability(this WebApplicationBuilder builder)
        {
            IServiceCollection services = builder.Services;

            // AgrumyMetrics is a singleton because its ConcurrentDictionary aggregate must span every request/scope.
            services.AddSingleton<AgrumyMetrics>();
            services
                .AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database")
                .AddCheck<CacheHealthCheck>("cache");

            // AddCheck<T>() above resolves T through IHealthCheck's own ActivatorUtilities factory, which doesn't need T registered in DI - ServerHealthService injects these two directly as plain constructor params, which DOES need an explicit registration.
            services.AddTransient<DatabaseHealthCheck>();
            services.AddTransient<CacheHealthCheck>();

            // Not registered through AddHealthChecks() - these back the admin-only Server Health card, not the public /api/health liveness probe, and ServerHealthService decides per-request which of them are even relevant.
            services.AddTransient<MqttHealthCheck>();
            services.AddTransient<EmailHealthCheck>();
            services.AddTransient<FirmwareSourceHealthCheck>();
            services.AddTransient<WeatherHealthCheck>();
            services.AddTransient<GatewayHealthCheck>();
            services.AddTransient<BackgroundWorkersHealthCheck>();
            services.AddScoped<IServerHealthService, ServerHealthService>();

            // Listens on the same "Agrumy.Api" Meter the JSON /metrics endpoint already reads, so Prometheus/Grafana get the identical counters with no separate instrumentation.
            services.AddOpenTelemetry()
                .WithMetrics(metrics => metrics
                    .AddMeter(AgrumyMetrics.MeterName)
                    .AddPrometheusExporter());

            // ClearProviders first, or JSON console just stacks on top of the default plain-text provider CreateBuilder registers.
            builder.Logging.ClearProviders();
            if (builder.Environment.IsDevelopment())
            {
                builder.Logging.AddSimpleConsole(o =>
                {
                    o.SingleLine = true;
                    o.TimestampFormat = "HH:mm:ss ";
                });
            }
            else
            {
                builder.Logging.AddJsonConsole(o =>
                {
                    o.UseUtcTimestamp = true;
                    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                    o.IncludeScopes = true;
                });
            }
            builder.Logging.AddDebug();

            return builder;
        }
    }
}
