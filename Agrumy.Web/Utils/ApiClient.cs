using System.Text.Json;
using Refit;

namespace Agrumy.Web.Utils
{
    /// Raised when Agrumy.Api answers a call from the web app with a non-success status.
    public sealed class ApiException(int statusCode, string rawBody, bool isAuthChallenge = false)
        : Exception(ExtractMessage(statusCode, rawBody))
    {
        public int StatusCode { get; } = statusCode;

        /// Human-readable text for the UI: ProblemDetails' detail/title, DbErrorResponse's message, validation errors joined, or the raw body when it is a plain string.
        public string Body { get; } = ExtractMessage(statusCode, rawBody);

        /// The response body exactly as received, for callers that need the API's own JSON.
        public string RawBody { get; } = rawBody ?? "";

        /// True only when the response carried a WWW-Authenticate header - set exclusively by the JWT bearer challenge itself (missing/invalid/expired/revoked token), never by an [Authorize]'d action's own 401 for a business reason.
        public bool IsAuthChallenge { get; } = isAuthChallenge;

        private static string ExtractMessage(int statusCode, string? rawBody)
        {
            string body = rawBody?.Trim() ?? "";
            if (body.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("detail", out JsonElement detail) && detail.ValueKind == JsonValueKind.String)
                    {
                        return detail.GetString()!;
                    }
                    if (root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
                    {
                        return message.GetString()!;
                    }
                    if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var lines = errors.EnumerateObject()
                            .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray().Select(e => e.GetString()) : [p.Value.ToString()])
                            .Where(l => !string.IsNullOrWhiteSpace(l));
                        string joined = string.Join(" ", lines);
                        if (joined.Length > 0)
                        {
                            return joined;
                        }
                    }
                    if (root.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
                    {
                        return title.GetString()!;
                    }
                }
                catch (JsonException) { }
            }
            return string.IsNullOrWhiteSpace(body) ? $"API call failed ({statusCode})." : body;
        }
    }

    /// Refit configuration for the <see cref="Agrumy.Web.Dal.Interface.IApi"/> client.
    public static class RefitConfig
    {
        public static readonly RefitSettings Settings = new()
        {
            // Refit's default ContentSerializer adds a global JsonStringEnumConverter, which breaks DTOs that deliberately stay numeric - use plain JsonSerializerDefaults.Web instead.
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)),

            // Carry the response body (Refit's default exception only has the status line) so failures like "email already registered" reach the UI.
            ExceptionFactory = async response =>
            {
                if (response.IsSuccessStatusCode)
                {
                    return null;
                }

                string body;
                try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); }
                catch { body = response.ReasonPhrase ?? ""; }

                return new ApiException((int)response.StatusCode, body, response.Headers.WwwAuthenticate.Count > 0);
            },
        };
    }
}
