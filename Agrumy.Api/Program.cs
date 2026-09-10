using Agrumy.Api;
using Agrumy.Shared;
using Agrumy.Shared.Models;
using Asp.Versioning;
using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Commands;
using Agrumy.Api.Diagnostics;
using Agrumy.Api.Firmware;
using Agrumy.Dal;
using Agrumy.Api.Dal;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Filters;
using Agrumy.Api.Notifications;
using Agrumy.Api.Security;
using Agrumy.Shared.Security;
using Agrumy.Api.Weather;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using StackExchange.Redis;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

AgrumySettings settingsForBootCheck = AgrumySettings.Bind(builder.Configuration);
builder.Services.AddSingleton(Options.Create(settingsForBootCheck));

// A first boot with no DB connection string routes to the minimal setup wizard instead of the rest of this file, until an admin supplies one - see Agrumy.Api/Setup/SetupWizard.cs.
if (string.IsNullOrWhiteSpace(settingsForBootCheck.DefaultConnection))
{
    Agrumy.Api.Setup.SetupWizard.ConfigureServices(builder);
    var wizardApp = builder.Build();
    Agrumy.Api.Setup.SetupWizard.LogSetupToken(wizardApp.Services.GetRequiredService<ILogger<Program>>());
    Agrumy.Api.Setup.SetupWizard.MapEndpoints(wizardApp);
    await wizardApp.RunAsync();
    return;
}

builder.Services.AddScoped(sp =>
{
    AgrumySettings settings = sp.GetRequiredService<IOptions<AgrumySettings>>().Value;
    string connectionString = settings.DefaultConnection
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
    return new AgrumyDbContext(DbOptionsFactory.Build(DbProviderKindParser.Parse(settings.DatabaseProvider), connectionString));
});

var secureKey = builder.Configuration["JWT:SecureKey"];
if (string.IsNullOrEmpty(secureKey))
    throw new InvalidOperationException("JWT:SecureKey is missing in configuration.");

var jwtIssuer = builder.Configuration["JWT:Issuer"];
var jwtAudience = builder.Configuration["JWT:Audience"];
if (string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
    throw new InvalidOperationException("JWT:Issuer and JWT:Audience are missing in configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => {
        // Same factory JwtTokenProvider.ValidateToken uses - see its BuildValidationParameters for why.
        o.TokenValidationParameters = JwtTokenProvider.BuildValidationParameters(secureKey, jwtIssuer, jwtAudience);
        // A JWT is self-validating and cannot be un-issued before its own expiry, so a password change or Enabled->false only takes effect immediately via this extra per-request check.
        o.Events = new JwtBearerEvents { OnTokenValidated = TokenRevocationValidator.ValidateAsync };
    });

// Device-communication endpoints authenticate by apiId/apiKey (or the short-lived apiAuth session token), not a user JWT - see Agrumy.Api.Security.DeviceAuth.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(DeviceAuth.ApiKeyPolicy, p => p.AddRequirements(new DeviceApiKeyRequirement()));
    options.AddPolicy(DeviceAuth.SessionPolicy, p => p.AddRequirements(new DeviceSessionRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, DeviceApiKeyHandler>();
builder.Services.AddScoped<IAuthorizationHandler, DeviceSessionHandler>();

// Persisted outside ContentRootPath (bin/) - a server deploy wipes that dir on every publish, which would erase keys and make every stored secret (Mqtt/Email/WiFi passwords) unreadable.
try
{
    var configuredKeyPath = builder.Configuration["DataProtection:KeyPath"];
    var keyRingPath = string.IsNullOrWhiteSpace(configuredKeyPath)
        ? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "dataprotection-keys"))
        : Path.GetFullPath(configuredKeyPath);
    Directory.CreateDirectory(keyRingPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .SetApplicationName("Agrumy.Api");
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"[DataProtection] Could not set up persisted keys, falling back to ephemeral: {ex.Message}");
}
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

builder.Services.AddScoped<DataSeeder>();
builder.Services.AddScoped<ISystemRepository, SchemaBootstrapper>();
builder.Services.AddScoped<IServerConfigRepository, EfServerConfigRepository>();
builder.Services.AddScoped<ISsrfAllowlistRepository, EfSsrfAllowlistRepository>();
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ITenantRepository, EfTenantRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IDeviceRepository, EfDeviceRepository>();
builder.Services.AddScoped<IDeviceFarmUnitRepository, EfDeviceFarmUnitRepository>();
builder.Services.AddScoped<IFarmOpenfieldRepository, EfFarmOpenfieldRepository>();
builder.Services.AddScoped<IDeviceOutboxRepository, EfDeviceOutboxRepository>();
builder.Services.AddScoped<IFirmwareRepository, EfFirmwareRepository>();
builder.Services.AddScoped<ISensorDataRepository, EfSensorDataRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<IGatewayRepository, EfGatewayRepository>();
builder.Services.AddScoped<IDiscoveryRepository, EfDiscoveryRepository>();
builder.Services.AddScoped<IControllerDataRepository, EfControllerDataRepository>();
builder.Services.AddScoped<ISimulationRepository, EfSimulationRepository>();
builder.Services.AddScoped<IExperimentRepository, EfExperimentRepository>();
builder.Services.AddScoped<IHorticultureCatalogRepository, EfHorticultureCatalogRepository>();

// Cache:Redis:ConnectionString switches to Redis; unset/empty keeps the in-process default.
string? redisConnectionString = builder.Configuration["Cache:Redis:ConnectionString"];
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddDistributedMemoryCache();

    // Single replica means no other process to race a background-worker tick against.
    builder.Services.AddSingleton<IDistributedLock, NoOpDistributedLock>();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
    builder.Services.AddSingleton<IDistributedLock, RedisDistributedLock>();
}
builder.Services.AddScoped<ICache, CacheRepository>();
builder.Services.AddScoped<DbExceptionFilter>();

// FCM push channel is registered but stays inert until the Android app registers device tokens - see FcmPushNotificationChannel.
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, FcmPushNotificationChannel>();
builder.Services.AddScoped<INotificationChannel, WebhookNotificationChannel>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

// A pooled HttpClient's handler outlives any one request, so its ConnectCallback can't just capture a scoped ISsrfAllowlistRepository - it opens its own short-lived scope on every connection instead, reading whatever the admin has saved at that moment.
static Func<CancellationToken, Task<IReadOnlyList<SsrfAllowlistEntry>>> SsrfAllowlistProvider(IServiceProvider rootServices, Func<ISsrfAllowlistRepository, Task<IReadOnlyList<SsrfAllowlistEntry>>> read) =>
    async _ =>
    {
        using IServiceScope scope = rootServices.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<ISsrfAllowlistRepository>());
    };

// AllowAutoRedirect=false: a redirect response is treated as a delivery failure rather than followed, same SsrfGuard-bypass concern as HttpFirmwareFetcher below, simpler to just refuse it here.
// ConnectCallback (not the plain HttpClientHandler this used to be) fixes a DNS-rebinding gap - it resolves+validates the host exactly once and dials that same address, instead of validating one lookup then letting SocketsHttpHandler silently re-resolve for the real connect.
builder.Services.AddHttpClient(WebhookNotificationChannel.ClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Agrumy.Api/1.0 (+https://github.com/dopiskur/AgrumyService)");
})
.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    ConnectCallback = SsrfGuard.CreateConnectCallback(SsrfAllowlistProvider(sp, r => r.WebhookAllowlistGetAllAsync())),
});

// Scoped, not singleton: it resolves scoped repositories/dispatcher itself, and PeriodicBackgroundService creates a fresh DI scope per tick.
builder.Services.AddScoped<OfflineAlertEvaluator>();
builder.Services.AddHostedService<OfflineAlertBackgroundService>();

builder.Services.AddScoped<LowBatteryAlertEvaluator>();
builder.Services.AddHostedService<LowBatteryAlertBackgroundService>();

builder.Services.AddScoped<TankRefillAlertEvaluator>();
builder.Services.AddHostedService<TankRefillAlertBackgroundService>();

// Roadmap #212: server-side evaluator for Notification-action rules - a Relay-action rule's fold runs on-device instead.
builder.Services.AddScoped<RuleNotificationEvaluator>();
builder.Services.AddHostedService<RuleNotificationBackgroundService>();

// MariaDB retention runs here; PostgreSQL/TimescaleDB installs use EfRepository.ApplyRetentionPolicyAsync (a native TimescaleDB policy) instead.
builder.Services.AddScoped<SensorDataRetentionEvaluator>();
builder.Services.AddHostedService<SensorDataRetentionBackgroundService>();

// Roadmap #409: opt-in (ServerConfig.PurgeOrphanedSensorDataScheduleEnabled) daily companion to the manual "Purge orphaned sensor data" trigger on DataMaintenanceApiController.
builder.Services.AddScoped<PurgeOrphanedSensorDataEvaluator>();
builder.Services.AddHostedService<PurgeOrphanedSensorDataBackgroundService>();

// Roadmap #209: MariaDB/MySQL-only archive to a separate admin-configured database, opt-in, no-op unless ServerConfig.ArchiveEnabled - moves rows instead of deleting them, an alternative to (not a replacement for) the retention purge above.
builder.Services.AddScoped<SensorDataArchiveEvaluator>();
builder.Services.AddHostedService<SensorDataArchiveBackgroundService>();

// Roadmap #403's hard 48h simulation-session safety cutoff.
builder.Services.AddScoped<SimulationSessionExpiryEvaluator>();
builder.Services.AddHostedService<SimulationSessionExpiryBackgroundService>();

builder.Services.AddScoped<DeviceOutboxRetentionEvaluator>();
builder.Services.AddHostedService<DeviceOutboxRetentionBackgroundService>();

builder.Services.AddScoped<DeviceOutboxDispatchEvaluator>();
builder.Services.AddHostedService<DeviceOutboxDispatchBackgroundService>();

builder.Services.AddScoped<TenantUsageSnapshotEvaluator>();
builder.Services.AddHostedService<TenantUsageSnapshotBackgroundService>();

builder.Services.AddHttpClient<IWeatherForecastClient, OpenWeatherMapClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<WeatherEvaluator>();
builder.Services.AddHostedService<WeatherBackgroundService>();

// Singleton, one persistent connection reused across every publish - see MqttConnectionManager's own remarks.
builder.Services.AddSingleton<MQTTnet.Client.IMqttClient>(_ => new MQTTnet.MqttFactory().CreateMqttClient());
builder.Services.AddSingleton<Agrumy.Api.Commands.IMqttConnectionManager, Agrumy.Api.Commands.MqttConnectionManager>();
builder.Services.AddScoped<Agrumy.Api.Commands.IMqttCommandPublisher, Agrumy.Api.Commands.MqttCommandPublisher>();
builder.Services.AddScoped<DeviceOutboxService>();
builder.Services.AddScoped<ManualActuateService>();
builder.Services.AddScoped<Agrumy.Api.Devices.DeviceConfigBuilder>();
builder.Services.AddScoped<Agrumy.Api.Devices.RuleValidationService>();
builder.Services.AddScoped<Agrumy.Api.Devices.RuleScopeConflictService>();
builder.Services.AddScoped<Agrumy.Api.Migration.TenantExportService>();
builder.Services.AddScoped<Agrumy.Api.Migration.TenantImportService>();
builder.Services.AddScoped<Agrumy.Api.Quota.TenantQuotaEnforcer>();

// Singleton so it outlives any one request's DI scope; BackgroundJobRunner consumes it one job at a time.
builder.Services.AddSingleton<BackgroundJobQueue>();
builder.Services.AddHostedService<BackgroundJobRunner>();

builder.Services.AddHttpClient(HttpFirmwareFetcher.ClientName, client =>
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
builder.Services.AddSingleton<IFirmwareFetcher, HttpFirmwareFetcher>();
builder.Services.AddSingleton<FirmwareStorage>();
builder.Services.AddScoped<FirmwareCatalogService>();

builder.Services.AddScoped<FirmwareCatalogRefreshEvaluator>();
builder.Services.AddHostedService<FirmwareCatalogRefreshBackgroundService>();

// BaseAddress is the server's OWN bound address (the same "Urls" config key Kestrel itself binds to), NOT the public WebView:ApiService URL (roadmap #396(10)) - routing a virtual device's self-loopback traffic out through the public internet just to call itself back is wasteful, depends on this server's own public hostname being reachable from itself, and would even count against its own public-facing rate limiters (#396(9)). Falls back to the documented local dev default if "Urls" is somehow unset.
builder.Services.AddHttpClient(VirtualDeviceRunnerBackgroundService.HttpClientName, (sp, client) =>
{
    string urls = sp.GetRequiredService<IConfiguration>()["Urls"] ?? "http://localhost:5000";
    string firstUrl = urls.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "http://localhost:5000";
    client.BaseAddress = new Uri(firstUrl);
});
builder.Services.AddSingleton<Agrumy.Api.Simulation.SimulatedSensorGenerator>();
builder.Services.AddHostedService<VirtualDeviceRunnerBackgroundService>();

// AgrumyMetrics is a singleton because its ConcurrentDictionary aggregate must span every request/scope, unlike the AddScoped registrations above.
builder.Services.AddSingleton<AgrumyMetrics>();
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<CacheHealthCheck>("cache");

// AddCheck<T>() above resolves T through IHealthCheck's own ActivatorUtilities factory, which doesn't need T registered in DI - ServerHealthService injects these two directly as plain constructor params, which DOES need an explicit registration.
builder.Services.AddTransient<DatabaseHealthCheck>();
builder.Services.AddTransient<CacheHealthCheck>();

// Not registered through AddHealthChecks() - these back the admin-only Server Health card (roadmap #419), not the public /api/health liveness probe, and ServerHealthService decides per-request which of them are even relevant (only currently-enabled integrations).
builder.Services.AddTransient<MqttHealthCheck>();
builder.Services.AddTransient<EmailHealthCheck>();
builder.Services.AddTransient<FirmwareSourceHealthCheck>();
builder.Services.AddTransient<WeatherHealthCheck>();
builder.Services.AddTransient<GatewayHealthCheck>();
builder.Services.AddTransient<BackgroundWorkersHealthCheck>();
builder.Services.AddScoped<IServerHealthService, ServerHealthService>();

// Listens on the same "Agrumy.Api" Meter the JSON /metrics endpoint already reads, so Prometheus/Grafana get the identical counters with no separate instrumentation.
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(AgrumyMetrics.MeterName)
        .AddPrometheusExporter());

builder.Services.AddControllers(options => options.Filters.AddService<DbExceptionFilter>());

// AssumeDefaultVersionWhenUnspecified keeps every existing caller (device firmware, Agrumy.Web's Refit client) working unversioned on 1.0; a future breaking change adds its own [ApiVersion("2.0")] controller instead of altering this one.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("X-Api-Version"));
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
})
.AddOpenApi(options =>
{
    options.Document.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Agrumy Web API";

        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Please enter valid JWT"
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    });
});

// KnownProxies must list only real proxy IPs (Security:KnownProxies) - trusting an arbitrary peer would let any client spoof X-Forwarded-For to bypass rate limiting and forge its apparent IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    string? configuredProxies = builder.Configuration["Security:KnownProxies"];
    if (!string.IsNullOrWhiteSpace(configuredProxies))
    {
        foreach (string proxy in configuredProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(proxy, out IPAddress? ip))
            {
                options.KnownProxies.Add(ip);
            }
        }
    }
});

// Rate limiting - all policies are fixed-window, partitioned by client IP, reject with 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // A fixed-window limiter's RetryAfter metadata is exact, so a caller that honors it - notably Agrumy.Gateway, forwarding it to devices as a "Wait" signal - gets a real number instead of guessing a backoff.
    options.OnRejected = (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }
        return ValueTask.CompletedTask;
    };

    static RateLimitPartition<string> IpFixedWindow(HttpContext httpContext, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

    // Human interactive login/registration - strict, this is the credential-stuffing target.
    options.AddPolicy("login", httpContext => IpFixedWindow(httpContext, 5));

    // Device onboarding/auth/config poll: ~2 req/min per device; 20/min/IP covers ~10 devices behind one NAT plus retries.
    options.AddPolicy("device-auth", httpContext => IpFixedWindow(httpContext, 20));

    // Device telemetry push: ~1/min per device; higher ceiling for many devices behind one NAT and catch-up bursts.
    options.AddPolicy("device-data", httpContext => IpFixedWindow(httpContext, 60));
});

builder.Services.AddEndpointsApiExplorer();

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

// HTTPS enforcement is opt-out via Security:EnforceHttps=false (default true), e.g. while firmware is still on http://.
bool enforceHttps = !bool.TryParse(builder.Configuration["Security:EnforceHttps"], out var eh) || eh;

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

// Must run before anything that reads Connection.RemoteIpAddress or Request.Scheme - the rate limiter below, but also UseHttpsRedirection/UseHsts further down.
app.UseForwardedHeaders();

// UseSwaggerUI just renders the Microsoft.AspNetCore.OpenApi-generated document; WithDocumentPerVersion generates one per discovered API version instead of a single hardcoded "v1".
app.MapOpenApi().WithDocumentPerVersion();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Agrumy Web API v1"));

// UseHsts only outside Development so local HTTP dev without a cert still works; UseHttpsRedirection is a no-op in dev with no https port.
if (enforceHttps)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseRouting();

// After UseRouting (needs the matched route pattern) but before rate limiting/auth/the endpoint itself, so recorded duration covers the whole request.
app.UseMiddleware<RequestMetricsMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// After auth resolves apiId/TenantID, so every log line the endpoint emits during this request carries them without each call site passing them explicitly.
app.UseMiddleware<Agrumy.Api.Diagnostics.LoggingScopeMiddleware>();
app.MapControllers();

// Under /api/ (roadmap #396(11)) - #387's path-based nginx/Apache templates route everything else to Agrumy.Web, so a root-level /health or /metrics would have been silently misrouted to the wrong service under that deploy mode. Unauthenticated on purpose: restart/deploy probes and external uptime monitors need to reach this without a JWT; it exposes only up/down + which dependency, nothing sensitive.
app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteResponse });

app.MapGet("/api/metrics", (AgrumyMetrics metrics) => Results.Json(metrics.GetSnapshot()))
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.MetricsReaders));

// Same JWT policy as the JSON endpoint above; point Prometheus's scrape config at this path with that bearer token (no separate secret to manage).
app.MapPrometheusScrapingEndpoint("/api/metrics/prometheus")
    .RequireAuthorization(policy => policy.RequireRole(RoleNames.MetricsReaders));

// Run the DB check at startup, not lazily on first request, so a bad connection string shows in deploy logs; Startup:FailFastOnDbCheck controls stop-vs-warn.
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var systemRepository = scope.ServiceProvider.GetRequiredService<ISystemRepository>();
    bool failFastOnDbCheck = bool.TryParse(builder.Configuration["Startup:FailFastOnDbCheck"], out var failFast) && failFast;

    try
    {
        if (await systemRepository.TestConnectionAsync())
        {
            startupLogger.LogInformation("Startup DB check: database connection OK.");
            await systemRepository.EnsureSchemaAsync();
            startupLogger.LogInformation("Startup DB check: schema verified/provisioned.");

            if (scope.ServiceProvider.GetRequiredService<IOptions<AgrumySettings>>().Value.ServerConfigReload)
            {
                await scope.ServiceProvider.GetRequiredService<IServerConfigRepository>().ServerConfigReloadFromAppSettingsAsync(1);
                startupLogger.LogInformation("ServerConfig:Reload was true - serverConfig hysteresis fields overwritten from appsettings.json.");
            }
        }
        else
        {
            const string message = "Startup DB check: could not open a database connection.";
            if (failFastOnDbCheck)
                throw new InvalidOperationException(message);
            startupLogger.LogError(message);
        }
    }
    catch (Exception ex) when (!failFastOnDbCheck)
    {
        startupLogger.LogError(ex, "Startup DB check failed; continuing because Startup:FailFastOnDbCheck is false.");
    }
}

app.Run();

namespace Agrumy.Api
{
    /// Marker type for Agrumy.Api.Tests' WebApplicationFactory - a dedicated type instead of the
    /// implicit top-level Program avoids a CS0433 clash with Agrumy.Web's own Program once both
    /// assemblies are referenced by the same test project.
    public sealed class ApiHostMarker { }
}
