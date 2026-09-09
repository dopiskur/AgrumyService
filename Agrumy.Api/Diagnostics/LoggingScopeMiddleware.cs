using Agrumy.Api.Security;

namespace Agrumy.Api.Diagnostics
{
    /// Must run after UseAuthorization (needs the device apiId set by DeviceAuth.DeviceApiId / the TenantID JWT claim) so every log line the endpoint emits carries them via ILogger scope, without each call site passing them explicitly.
    internal sealed class LoggingScopeMiddleware(RequestDelegate next, ILogger<LoggingScopeMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var scopeState = new Dictionary<string, object?>();
            string? apiId = context.DeviceApiId();
            if (apiId is not null)
            {
                scopeState["ApiId"] = apiId;
            }
            string? tenantId = (context.User.Identity as System.Security.Claims.ClaimsIdentity)?.FindFirst("TenantID")?.Value;
            if (tenantId is not null)
            {
                scopeState["TenantId"] = tenantId;
            }

            if (scopeState.Count == 0)
            {
                await next(context);
                return;
            }

            using (logger.BeginScope(scopeState))
            {
                await next(context);
            }
        }
    }
}
