using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Agrumy.Api.Dal.Interface;

namespace Agrumy.Api.Satellite
{
    public interface ICdseTokenProvider
    {
        /// Null means "not configured" or "token request failed" (already logged) - callers treat both as "skip this tenant this tick", never throw.
        Task<string?> GetAccessTokenAsync(int tenantId, CancellationToken ct);

        /// Bypasses the per-tenant cache/DB entirely - the "Test connection" endpoint's own path for possibly-unsaved credentials (Agrumy.Api.Controllers.API.TenantApiController.SatelliteConfigTest), never written to LastTokenIssuedUtc.
        Task<(bool Ok, string? Error)> TryGetAccessTokenForCredentialsAsync(string clientId, string clientSecret, CancellationToken ct);
    }

    /// Client-credentials OAuth against CDSE's identity server (Detaljni dizajn S, B1) - cached per tenant with TTL = expires_in - 60s, refreshed on the next call once expired. Never puts the token on HttpClient's own default headers (that instance is shared/pooled across every tenant); callers attach it per-request instead.
    public sealed class CdseTokenProvider(HttpClient httpClient, ISatelliteConfigRepository configRepo, Agrumy.Api.Dal.Interface.ICache cache, ILogger<CdseTokenProvider> logger) : ICdseTokenProvider
    {
        public const string TokenUrl = "https://identity.dataspace.copernicus.eu/auth/realms/CDSE/protocol/openid-connect/token";

        public async Task<string?> GetAccessTokenAsync(int tenantId, CancellationToken ct)
        {
            string cacheKey = $"satellite-token:{tenantId}";
            CachedToken? cached = await cache.GetAsync<CachedToken>(cacheKey);
            if (cached != null && cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                return cached.AccessToken;
            }

            (string? clientId, string? clientSecret) = await configRepo.SatelliteConfigCredentialsGetAsync(tenantId);
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                }),
            };

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "CDSE token request failed for tenant {TenantId}.", tenantId);
                return null;
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CDSE token request for tenant {TenantId} returned {StatusCode}.", tenantId, response.StatusCode);
                return null;
            }

            TokenResponse? payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (string.IsNullOrEmpty(payload?.AccessToken))
            {
                return null;
            }

            int ttlSeconds = Math.Max(30, payload.ExpiresIn - 60);
            var cachedToken = new CachedToken(payload.AccessToken, DateTimeOffset.UtcNow.AddSeconds(ttlSeconds));
            await cache.SetAsync(cacheKey, cachedToken, TimeSpan.FromSeconds(ttlSeconds));
            await configRepo.SatelliteConfigTokenIssuedAsync(tenantId, DateTimeOffset.UtcNow);
            return payload.AccessToken;
        }

        public async Task<(bool Ok, string? Error)> TryGetAccessTokenForCredentialsAsync(string clientId, string clientSecret, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                }),
            };
            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                return (false, ex.Message);
            }
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }
            string body = await response.Content.ReadAsStringAsync(ct);
            return (false, $"{(int)response.StatusCode} {response.ReasonPhrase}: {(body.Length > 300 ? body[..300] : body)}");
        }

        private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
}
