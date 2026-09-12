using Agrumy.Api.Dal;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Security;
using Agrumy.Dal;
using Agrumy.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Agrumy.Api.Startup
{
    /// DbContext, the DataProtection key ring, every EF repository facet, and the distributed cache/lock.
    public static class DataServiceExtensions
    {
        public static WebApplicationBuilder AddAgrumyData(this WebApplicationBuilder builder)
        {
            IServiceCollection services = builder.Services;

            services.AddScoped(sp =>
            {
                AgrumySettings settings = sp.GetRequiredService<IOptions<AgrumySettings>>().Value;
                string connectionString = settings.DefaultConnection
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
                return new AgrumyDbContext(DbOptionsFactory.Build(DbProviderKindParser.Parse(settings.DatabaseProvider), connectionString));
            });

            // Persisted outside ContentRootPath (bin/) - a server deploy wipes that dir on every publish, which would erase keys and make every stored secret (Mqtt/Email/WiFi passwords) unreadable.
            try
            {
                var configuredKeyPath = builder.Configuration["DataProtection:KeyPath"];
                var keyRingPath = string.IsNullOrWhiteSpace(configuredKeyPath)
                    ? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "dataprotection-keys"))
                    : Path.GetFullPath(configuredKeyPath);
                Directory.CreateDirectory(keyRingPath);
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                    .SetApplicationName("Agrumy.Api");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"[DataProtection] Could not set up persisted keys, falling back to ephemeral: {ex.Message}");
            }
            services.AddSingleton<ISecretProtector, SecretProtector>();

            services.AddScoped<DataSeeder>();
            services.AddScoped<ISystemRepository, SchemaBootstrapper>();
            services.AddScoped<IServerConfigRepository, EfServerConfigRepository>();
            services.AddScoped<ISsrfAllowlistRepository, EfSsrfAllowlistRepository>();
            services.AddScoped<IUserRepository, EfUserRepository>();
            services.AddScoped<ITenantRepository, EfTenantRepository>();
            services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
            services.AddScoped<IDeviceRepository, EfDeviceRepository>();
            services.AddScoped<IDeviceFarmUnitRepository, EfDeviceFarmUnitRepository>();
            services.AddScoped<ICropCatalogRepository, EfCropCatalogRepository>();
            services.AddScoped<IFarmParcelRepository, EfFarmParcelRepository>();
            services.AddScoped<ISowingRepository, EfSowingRepository>();
            services.AddScoped<IFieldLogRepository, EfFieldLogRepository>();
            services.AddScoped<IZonePlantingRepository, EfZonePlantingRepository>();
            services.AddScoped<ISatelliteConfigRepository, EfSatelliteConfigRepository>();
            services.AddScoped<ISatelliteSceneRepository, EfSatelliteSceneRepository>();
            services.AddScoped<IDeviceOutboxRepository, EfDeviceOutboxRepository>();
            services.AddScoped<IFirmwareRepository, EfFirmwareRepository>();
            services.AddScoped<ISensorDataRepository, EfSensorDataRepository>();
            services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
            services.AddScoped<IGatewayRepository, EfGatewayRepository>();
            services.AddScoped<IDiscoveryRepository, EfDiscoveryRepository>();
            services.AddScoped<IControllerDataRepository, EfControllerDataRepository>();
            services.AddScoped<ISimulationRepository, EfSimulationRepository>();
            services.AddScoped<IExperimentRepository, EfExperimentRepository>();
            services.AddScoped<IHorticultureCatalogRepository, EfHorticultureCatalogRepository>();

            // Cache:Redis:ConnectionString switches to Redis; unset/empty keeps the in-process default.
            string? redisConnectionString = builder.Configuration["Cache:Redis:ConnectionString"];
            if (string.IsNullOrWhiteSpace(redisConnectionString))
            {
                services.AddDistributedMemoryCache();

                // Single replica means no other process to race a background-worker tick against.
                services.AddSingleton<IDistributedLock, NoOpDistributedLock>();
            }
            else
            {
                services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
                services.AddSingleton<IDistributedLock, RedisDistributedLock>();
            }
            services.AddScoped<ICache, CacheRepository>();

            return builder;
        }
    }
}
