using System.Net.Http.Json;
using Agrumy.Shared.Models;
using Agrumy.Gateway.Registration;
using Microsoft.Extensions.Options;

namespace Agrumy.Gateway
{
    /// AgrumyService rate-limited this gateway's own Batch call (many devices share one egress IP) - carries RetryAfterSeconds so ProfileAEndpoints can turn this into a "Wait" response on the device's own config-poll instead of masking it as a generic failure.
    public class GatewayRateLimitedException(int retryAfterSeconds) : Exception
    {
        public int RetryAfterSeconds { get; } = retryAfterSeconds;
    }

    /// Thin HTTP client for the two calls a gateway makes to AgrumyService (registration once, Batch repeatedly) - plain typed HttpClient, not Refit, since two endpoints don't earn that ceremony.
    public class AgrumyServiceClient(HttpClient http, GatewayRegistrationStore registration, IOptions<GatewayOptions> options)
    {
        private readonly GatewaySelfOptions self = options.Value.Gateway;

        public async Task<GatewayRegistrationState> RegisterAsync(CancellationToken ct)
        {
            var request = new DeviceRegistration
            {
                Email = options.Value.AgrumyService.Email,
                DevicePin = options.Value.AgrumyService.DevicePin,
                MacAddress = self.MacAddress,
                IsGateway = true,
                GatewayProfile = self.Profile,
                GatewayRegistrationSecret = self.RegistrationSecret,
            };

            HttpResponseMessage response = await http.PostAsJsonAsync("/api/Device/Register", request, ct);
            response.EnsureSuccessStatusCode();
            DeviceConfig config = (await response.Content.ReadFromJsonAsync<DeviceConfig>(ct))
                ?? throw new InvalidOperationException("Register returned an empty body.");

            return new GatewayRegistrationState
            {
                ApiId = config.ApiId,
                ApiKey = config.ApiKey,
                IdDevice = config.deviceID,
            };
        }

        /// Authenticates with THIS gateway's own apiId/apiKey (DeviceAuth.ApiKeyPolicy) - re-sends the permanent credential on every Batch call rather than bootstrapping a short-lived apiAuth session first.
        public async Task<GatewayBatchResponse> BatchAsync(GatewayBatchRequest request, CancellationToken ct)
        {
            GatewayRegistrationState reg = registration.Current;
            if (!reg.IsComplete)
            {
                throw new InvalidOperationException("Gateway is not registered yet.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/Gateway/Batch")
            {
                Content = JsonContent.Create(request),
            };
            message.Headers.Add("apiId", reg.ApiId);
            message.Headers.Add("apiKey", reg.ApiKey);

            HttpResponseMessage response = await http.SendAsync(message, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                int retryAfter = response.Headers.RetryAfter?.Delta is TimeSpan delta ? Math.Clamp((int)delta.TotalSeconds, 10, 300) : 30; // same 10-300s clamp as ServerConfigApiController.Update
                throw new GatewayRateLimitedException(retryAfter);
            }
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GatewayBatchResponse>(ct))
                ?? new GatewayBatchResponse();
        }

        /// LoRaPrivateProtocolUplinkService forwards the still-encrypted uplink bytes here instead of decrypting locally (same "gateway holds no permanent per-device secret" principle as GatewayDeviceToken); AgrumyService is the only place that ever sees the plaintext or the key. Same apiId/apiKey auth as Batch.
        public async Task<GatewayBatchEntryResult> RelayUplinkAsync(GatewayRelayUplinkRequest request, CancellationToken ct)
        {
            GatewayRegistrationState reg = registration.Current;
            if (!reg.IsComplete)
            {
                throw new InvalidOperationException("Gateway is not registered yet.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/Gateway/RelayUplink")
            {
                Content = JsonContent.Create(request),
            };
            message.Headers.Add("apiId", reg.ApiId);
            message.Headers.Add("apiKey", reg.ApiKey);

            HttpResponseMessage response = await http.SendAsync(message, ct);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GatewayBatchEntryResult>(ct))
                ?? new GatewayBatchEntryResult { Success = false, StatusCode = 502, Error = "Empty response from AgrumyService." };
        }

        /// This gateway's own DevEUI->device mapping (Profile B) - GET /api/Gateway/DeviceMapping, same apiId/apiKey auth as Batch.
        public async Task<IList<GatewayDeviceMapping>> GetDeviceMappingAsync(CancellationToken ct)
        {
            GatewayRegistrationState reg = registration.Current;
            if (!reg.IsComplete)
            {
                return [];
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, "/api/Gateway/DeviceMapping");
            message.Headers.Add("apiId", reg.ApiId);
            message.Headers.Add("apiKey", reg.ApiKey);

            HttpResponseMessage response = await http.SendAsync(message, ct);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<IList<GatewayDeviceMapping>>(ct)) ?? [];
        }
    }
}
