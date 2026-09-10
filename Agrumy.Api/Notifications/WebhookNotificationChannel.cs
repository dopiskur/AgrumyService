using System.Security.Cryptography;
using System.Text;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Notifications
{
    /// Posts a JSON event to an operator-configured URL, so an external system learns about an alert without polling Agrumy. Config lives in the DB-backed ServerConfig (Webhook* fields, admin-editable via Server Settings), not appsettings - read fresh on every call, same pattern as EmailNotificationChannel.
    public sealed class WebhookNotificationChannel(IServerConfigRepository serverConfigRepo, IHttpClientFactory httpClientFactory, ILogger<WebhookNotificationChannel> logger) : INotificationChannel
    {
        public const string ClientName = "WebhookNotificationChannel";

        public string Name => "webhook";
        public bool PerRecipient => false;

        private static bool IsConfigured(ServerConfig config) =>
            config.WebhookEnabled
            && Uri.TryCreate(config.WebhookUrl, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps;

        public async Task<bool> IsConfiguredAsync(CancellationToken ct = default) =>
            IsConfigured(await serverConfigRepo.ServerConfigGetAsync(1));

        public async Task<NotificationResult> SendAsync(Notification notification, CancellationToken ct = default)
        {
            if (notification.ContainsSecret)
            {
                return NotificationResult.Skipped("notification carries a secret, never forwarded to webhook");
            }
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (!IsConfigured(config))
            {
                return NotificationResult.Skipped("webhook channel disabled or Url missing/not https");
            }

            var uri = new Uri(config.WebhookUrl!); // https-scheme, valid absolute: IsConfigured
            try
            {
                await SsrfGuard.EnsureAllowedAsync(uri, ct);
            }
            catch (SsrfBlockedException ex)
            {
                logger.LogWarning(ex, "Webhook notification blocked by SsrfGuard.");
                return NotificationResult.Failed(ex.Message);
            }

            var payload = new WebhookPayload(notification.Subject, notification.Body, notification.Severity.ToString(), DateTime.UtcNow);
            byte[] body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);

            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new ByteArrayContent(body)
            };
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            if (!string.IsNullOrEmpty(config.WebhookSecret))
            {
                request.Headers.Add("X-Agrumy-Signature", ComputeSignature(body, config.WebhookSecret));
            }

            try
            {
                HttpClient client = httpClientFactory.CreateClient(ClientName);
                using HttpResponseMessage response = await client.SendAsync(request, ct);
                return response.IsSuccessStatusCode
                    ? NotificationResult.Ok($"POST {uri} -> {(int)response.StatusCode}")
                    : NotificationResult.Failed($"POST {uri} -> {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Webhook notification to {Url} failed.", uri);
                return NotificationResult.Failed(ex.Message);
            }
        }

        internal static string ComputeSignature(byte[] body, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
        }

        private sealed record WebhookPayload(string Subject, string Body, string Severity, DateTime TimestampUtc);
    }
}
