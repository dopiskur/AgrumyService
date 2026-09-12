using Agrumy.Api.Filters;
using Asp.Versioning;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using System.Net;
using System.Threading.RateLimiting;

namespace Agrumy.Api.Startup
{
    /// MVC/OData controllers, API versioning + OpenAPI, forwarded headers, rate limiting and HSTS.
    public static class WebApiServiceExtensions
    {
        public static WebApplicationBuilder AddAgrumyWebApi(this WebApplicationBuilder builder)
        {
            IServiceCollection services = builder.Services;

            services.AddScoped<DbExceptionFilter>();

            // Query-only OData ($filter/$select/$orderby/$top/$count) for SensorDataODataController's [EnableQuery] action - no EDM model/route-component registration needed since that action already returns a plain IQueryable. AV0022 (suppressed project-wide, see the .csproj) wants this routed through Asp.Versioning's own OData package instead, which needs a full per-version EDM model this single, version-neutral feed doesn't - see the controller's own [ApiVersionNeutral].
            services.AddControllers(options => options.Filters.AddService<DbExceptionFilter>())
                .AddOData(o => o.Select().Filter().OrderBy().SetMaxTop(5000).Count());

            // AssumeDefaultVersionWhenUnspecified keeps every existing caller (device firmware, Agrumy.Web's Refit client) working unversioned on 1.0; a future breaking change adds its own [ApiVersion("2.0")] controller instead of altering this one.
            services.AddApiVersioning(options =>
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
            services.Configure<ForwardedHeadersOptions>(options =>
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
            services.AddRateLimiter(options =>
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

            services.AddEndpointsApiExplorer();

            services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
            });

            return builder;
        }
    }
}
