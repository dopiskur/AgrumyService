using System.Security.Cryptography;

namespace Agrumy.Web.Security
{
    /// Sets response headers with no per-route variation, so plain response mutation (not endpoint-specific) is enough - runs early, before routing.
    internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "same-origin";

            // Per-request nonce (not unsafe-inline) for script-src - views read it via HttpContext.CspNonce() and stamp it on every legitimate inline <script>, so an injected script (also "inline") is rejected for lacking it. style-src stays unsafe-inline for now (lower-severity, out of scope here).
            string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[CspNonceExtensions.ItemKey] = nonce;
            // img-src/connect-src carry the ARKOD WMS (servisi.apprrr.hr) and Esri World Imagery (server.arcgisonline.com) map tile hosts - parcel-geometry-map.js/satellite-map.js hit these directly from the browser, not through Agrumy.Api's TileProxy.
            context.Response.Headers["Content-Security-Policy"] =
                $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://servisi.apprrr.hr https://server.arcgisonline.com; font-src 'self'; connect-src 'self' https://servisi.apprrr.hr; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

            await next(context);
        }
    }

    internal static class CspNonceExtensions
    {
        public const string ItemKey = "CspNonce";

        public static string CspNonce(this HttpContext context) => (string)context.Items[ItemKey]!;
    }
}
