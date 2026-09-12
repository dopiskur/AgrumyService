using Agrumy.Api.Arkod;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Storage;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Offline/manual-upload path for the local ARKOD GeoPackage mirror - the primary WMS click-lookup (parcel-geometry-map.js) talks to servisi.apprrr.hr directly from the browser and never touches this controller (CORS is open on that endpoint). No organization/ownership scoping here - ARKOD parcel data is a public national registry, not organization data. Plain ControllerBase, not ApiControllerBase - no audit-log/cache dependency to justify that base's constructor.
    [ApiController]
    [ApiVersion("1.0")]
    [Route("/api/Arkod")]
    public class ArkodApiController(ArkodGeoPackageLookup lookup, ArkodGeoPackageStorage storage, IServerConfigRepository serverConfigRepo) : ControllerBase
    {
        [Authorize]
        [HttpGet("Lookup")]
        public async Task<ActionResult<ArkodParcelLookupResult>> Lookup(string jpaid, CancellationToken ct)
        {
            if (!storage.Exists)
            {
                return StatusCode(503, "No local ARKOD GeoPackage mirror yet - enable sync or upload one on Server Settings.");
            }
            ArkodParcelLookupResult? result = await lookup.TryFindByJpaIdAsync(jpaid, ct);
            return result == null ? NotFound() : Ok(result);
        }

        // ~900 MB whole-Croatia export - the offline-deployment fallback for when the sync job can't reach the internet, so this stays generous rather than the firmware-upload endpoints' few-MB caps.
        private const long MaxUploadBytes = 1_200_000_000;

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost("GeoPackage/Upload")]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<ActionResult> GeoPackageUpload(IFormFile file, CancellationToken ct)
        {
            if (file.Length == 0)
            {
                return BadRequest("Choose a .gpkg file first.");
            }
            await using Stream stream = file.OpenReadStream();
            await storage.SaveFromStreamAsync(stream, ct);
            // Same "last synced" field the automatic job writes - a manual upload is meant to be an equivalent, not a second, untracked path.
            await serverConfigRepo.ServerConfigArkodSyncStateSetAsync(DateTimeOffset.UtcNow, 1);
            return Ok();
        }
    }
}
