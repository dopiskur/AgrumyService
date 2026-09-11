using Agrumy.Api.Controllers.API;
using Agrumy.Api.Dal.Interface;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Map
{
    /// Server-side cache+proxy for OSM basemap tiles - browsers hotlinking tile.openstreetmap.org directly pushed the whole install over OSMF's Tile Usage Policy (~2 req/s per source, no way to enforce that client-side across every open tab/device), which got the install blocklisted. This makes the SERVER the one identified, rate-limited source instead - same disk-cache-then-fetch-on-miss shape as FirmwareStorage/SatelliteStorage. Anonymous access would turn this into an open tile mirror for anyone, so it stays behind the same [Authorize] every other API action needs.
    [Route("api/Map/Tile")]
    [ApiVersionNeutral]
    [Authorize]
    public class TileProxy(TileStorage storage, OsmTileRateLimiter rateLimiter, IHttpClientFactory httpClientFactory, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, ILogger<TileProxy> logger)
        : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        public const string ClientName = "OsmTile";

        [HttpGet("{z:int}/{x:int}/{y:int}.png")]
        public async Task<ActionResult> Get(int z, int x, int y, CancellationToken ct)
        {
            if (z < 0 || z > 19)
            {
                return BadRequest("z must be 0-19.");
            }
            long span = 1L << z; // valid x/y range at this zoom is [0, 2^z) - also rejects a path-traversal attempt disguised as a huge/negative coordinate
            if (x < 0 || y < 0 || x >= span || y >= span)
            {
                return BadRequest("x/y out of range for this z.");
            }

            byte[]? cached = storage.TryRead(z, x, y);
            if (cached != null)
            {
                return File(cached, "image/png");
            }

            await rateLimiter.WaitAsync(ct);
            HttpClient client = httpClientFactory.CreateClient(ClientName);
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync($"https://tile.openstreetmap.org/{z}/{x}/{y}.png", ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "OSM tile fetch failed for {Z}/{X}/{Y}.", z, x, y);
                return StatusCode(502);
            }
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }

            byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);
            await storage.SaveAsync(z, x, y, bytes, ct);
            return File(bytes, "image/png");
        }
    }
}
