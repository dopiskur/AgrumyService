using System.Security.Cryptography;
using System.Text;

namespace Agrumy.Api.Security
{
    /// A gateway-scoped, short-lived credential (GET /api/Gateway/DeviceMapping hands this out instead of a mapped device's real, permanent ApiKey, so a compromised gateway leaks only a time-boxed proof rather than every leaf device's actual credential. GatewayApiController.RunEntryAsync accepts this OR a real ApiKey, since a WiFiRepeater-profile device still sends its own genuine credential directly, unrelated to this path.
    public static class GatewayDeviceToken
    {
        // Comfortably longer than ChirpStackUplinkService/LoRaPrivateProtocolUplinkService's MappingRefreshSeconds poll (default 60s) - a fresh token always arrives well before the previous one could expire.
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

        public static string Issue(string apiId, string apiKey)
        {
            long expiresAtUnixSeconds = DateTimeOffset.UtcNow.Add(Ttl).ToUnixTimeSeconds();
            return $"{expiresAtUnixSeconds}.{Sign(apiId, apiKey, expiresAtUnixSeconds)}";
        }

        public static bool Verify(string? token, string apiId, string apiKey)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }
            int dot = token.IndexOf('.');
            if (dot <= 0 || !long.TryParse(token.AsSpan(0, dot), out long expiresAtUnixSeconds))
            {
                return false;
            }
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAtUnixSeconds)
            {
                return false;
            }
            return DeviceAuth.ConstantTimeEquals(token[(dot + 1)..], Sign(apiId, apiKey, expiresAtUnixSeconds));
        }

        private static string Sign(string apiId, string apiKey, long expiresAtUnixSeconds)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{apiId}:{expiresAtUnixSeconds}")));
        }
    }
}
