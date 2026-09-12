using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Diagnostics;
using Agrumy.Api.Security;
using Agrumy.Shared;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Startup
{
    /// The request pipeline in its required order, the non-controller endpoints, and the startup DB check.
    public static class PipelineExtensions
    {
        public static WebApplication UseAgrumyPipeline(this WebApplication app)
        {
            // HTTPS enforcement is opt-out via Security:EnforceHttps=false (default true), e.g. while firmware is still on http://.
            bool enforceHttps = !bool.TryParse(app.Configuration["Security:EnforceHttps"], out var eh) || eh;

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
            app.UseMiddleware<LoggingScopeMiddleware>();
            app.MapControllers();

            // Under /api/ because the path-based nginx/Apache templates route everything else to Agrumy.Web. Unauthenticated on purpose: restart/deploy probes and uptime monitors need it without a JWT; it exposes only up/down + which dependency.
            app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteResponse });

            app.MapGet("/api/metrics", (AgrumyMetrics metrics) => Results.Json(metrics.GetSnapshot()))
                .RequireAuthorization(policy => policy.RequireRole(RoleNames.MetricsReaders));

            // Same JWT policy as the JSON endpoint above; point Prometheus's scrape config at this path with that bearer token (no separate secret to manage).
            app.MapPrometheusScrapingEndpoint("/api/metrics/prometheus")
                .RequireAuthorization(policy => policy.RequireRole(RoleNames.MetricsReaders));

            return app;
        }

        /// Runs the DB check at startup, not lazily on first request, so a bad connection string shows in deploy logs; Startup:FailFastOnDbCheck controls stop-vs-warn.
        public static async Task RunStartupDbCheckAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
            var systemRepository = scope.ServiceProvider.GetRequiredService<ISystemRepository>();
            bool failFastOnDbCheck = bool.TryParse(app.Configuration["Startup:FailFastOnDbCheck"], out var failFast) && failFast;

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
    }
}
