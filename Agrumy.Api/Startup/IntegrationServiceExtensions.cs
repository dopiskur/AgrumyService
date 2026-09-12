using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Agrumy.Api.Notifications;
using Agrumy.Api.Security;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Startup
{
    /// Everything that talks to the outside world: notification channels, satellite/ARKOD/OSM clients, the MQTT broker, and firmware fetching.
    public static class IntegrationServiceExtensions
    {
        public static WebApplicationBuilder AddAgrumyIntegrations(this WebApplicationBuilder builder)
        {
            IServiceCollection services = builder.Services;

            // FCM push channel is registered but stays inert until the Android app registers device tokens - see FcmPushNotificationChannel.
            services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
            services.AddScoped<INotificationChannel, EmailNotificationChannel>();
            services.AddScoped<INotificationChannel, FcmPushNotificationChannel>();
            services.AddScoped<INotificationChannel, WebhookNotificationChannel>();
            services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

            // AllowAutoRedirect=false: a redirect response is treated as a delivery failure rather than followed, same SsrfGuard-bypass concern as HttpFirmwareFetcher below, simpler to just refuse it here.
            // ConnectCallback (not a plain HttpClientHandler) fixes a DNS-rebinding gap - it resolves+validates the host exactly once and dials that same address, instead of validating one lookup then letting SocketsHttpHandler silently re-resolve for the real connect.
            services.AddHttpClient(WebhookNotificationChannel.ClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Agrumy.Api/1.0 (+https://github.com/dopiskur/AgrumyService)");
            })
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = SsrfGuard.CreateConnectCallback(SsrfAllowlistProvider(sp, r => r.WebhookAllowlistGetAllAsync())),
            });

            services.AddSingleton<Agrumy.Api.Storage.SatelliteStorage>();
            // Shared/pooled HttpClient for both CDSE calls - CdseTokenProvider and CdseSentinelHubSource each attach Authorization per-request, never as a default header, so pooling never leaks one tenant's token onto another's request.
            services.AddHttpClient<Agrumy.Api.Satellite.ICdseTokenProvider, Agrumy.Api.Satellite.CdseTokenProvider>(client => client.Timeout = TimeSpan.FromSeconds(20));
            services.AddHttpClient<Agrumy.Api.Satellite.CdseSentinelHubSource>(client => client.Timeout = TimeSpan.FromSeconds(60));
            services.AddScoped<Agrumy.Api.Satellite.ISatelliteImagerySourceFactory, Agrumy.Api.Satellite.SatelliteImagerySourceFactory>();

            // ARKOD GeoPackage local mirror (secondary/offline path alongside the browser-side WMS click-lookup).
            services.AddSingleton<Agrumy.Api.Storage.ArkodGeoPackageStorage>();
            services.AddScoped<Agrumy.Api.Arkod.ArkodGeoPackageLookup>();

            // OSM tile proxy - OsmTileRateLimiter is a singleton on purpose (see its own remarks), one gate shared by every request regardless of DI scope.
            services.AddSingleton<Agrumy.Api.Map.TileStorage>();
            services.AddSingleton<Agrumy.Api.Map.OsmTileRateLimiter>();
            // OSMF's Tile Usage Policy requires an identifying User-Agent, not a generic HttpClient default.
            services.AddHttpClient(Agrumy.Api.Map.TileProxy.ClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Agrumy-TileProxy/1.0 (+https://github.com/dopiskur/AgrumyService)");
            });

            // Singleton, one persistent connection reused across every publish - see MqttConnectionManager's own remarks.
            services.AddSingleton<MQTTnet.Client.IMqttClient>(_ => new MQTTnet.MqttFactory().CreateMqttClient());
            services.AddSingleton<IMqttConnectionManager, MqttConnectionManager>();
            services.AddScoped<IMqttCommandPublisher, MqttCommandPublisher>();

            services.AddHttpClient(HttpFirmwareFetcher.ClientName, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5); // a full "pull from GitHub" streams several MB per file
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Agrumy.Api/1.0 (+https://github.com/dopiskur/AgrumyService)");
            })
            // AllowAutoRedirect=false: HttpFirmwareFetcher follows redirects itself so SsrfGuard re-validates every hop. ConnectCallback: see the Webhook registration's remark above, same DNS-rebinding fix.
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = SsrfGuard.CreateConnectCallback(SsrfAllowlistProvider(sp, r => r.FirmwareAllowlistGetAllAsync())),
            });
            services.AddSingleton<IFirmwareFetcher, HttpFirmwareFetcher>();
            services.AddSingleton<FirmwareStorage>();
            services.AddSingleton<Agrumy.Api.Storage.FieldLogAttachmentStorage>();
            services.AddScoped<FirmwareCatalogService>();

            return builder;
        }

        // A pooled HttpClient's handler outlives any one request, so its ConnectCallback can't just capture a scoped ISsrfAllowlistRepository - it opens its own short-lived scope on every connection instead, reading whatever the admin has saved at that moment.
        private static Func<CancellationToken, Task<IReadOnlyList<SsrfAllowlistEntry>>> SsrfAllowlistProvider(IServiceProvider rootServices, Func<ISsrfAllowlistRepository, Task<IReadOnlyList<SsrfAllowlistEntry>>> read) =>
            async _ =>
            {
                using IServiceScope scope = rootServices.CreateScope();
                return await read(scope.ServiceProvider.GetRequiredService<ISsrfAllowlistRepository>());
            };
    }
}
