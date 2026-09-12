using Agrumy.Api.Commands;

namespace Agrumy.Api.Startup
{
    /// Request-scoped domain services the controllers compose: command queue, manual actuation, config building, rule validation, organization migration and quota.
    public static class DomainServiceExtensions
    {
        public static WebApplicationBuilder AddAgrumyDomainServices(this WebApplicationBuilder builder)
        {
            IServiceCollection services = builder.Services;
            services.AddScoped<DeviceOutboxService>();
            services.AddScoped<ManualActuateService>();
            services.AddScoped<Agrumy.Api.Devices.DeviceConfigBuilder>();
            services.AddScoped<Agrumy.Api.Devices.RuleValidationService>();
            services.AddScoped<Agrumy.Api.Devices.RuleScopeConflictService>();
            services.AddScoped<Agrumy.Api.Migration.TenantExportService>();
            services.AddScoped<Agrumy.Api.Migration.TenantImportService>();
            services.AddScoped<Agrumy.Api.Quota.TenantQuotaEnforcer>();
            return builder;
        }
    }
}
