using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// CRUD over the six crop subcatalogs (Arable/Fruit/Vegetable/Industrial/Ornamental/MedicinalAndAromatic) - reads are open to any authenticated user (every organization browses the same shared catalog to apply a template), writes are Global Admin-only for now; a future public/community catalog (roadmap's own explicit "not now") would relax the read side further, not the write side.
    [Route("/api/CropCatalog")]
    [Authorize]
    public class CropCatalogApiController(ICropCatalogEntryRepository catalogRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [HttpGet]
        public async Task<ActionResult<IList<CropCatalogEntry>>> CatalogGet(CropCatalogType type) =>
            Ok(await catalogRepo.CatalogGetAsync(type));

        [HttpGet("ById")]
        public async Task<ActionResult<CropCatalogEntry>> CatalogGetById(CropCatalogType type, int id)
        {
            CropCatalogEntry? entry = await catalogRepo.CatalogGetByIdAsync(type, id);
            return entry is null ? NotFound() : Ok(entry);
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        public async Task<ActionResult<CropCatalogEntry>> CatalogAdd(CropCatalogType type, [FromBody] CropCatalogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                return BadRequest("Name is required.");
            }
            CropCatalogEntry added = await catalogRepo.CatalogAddAsync(type, entry);
            await WriteAuditAsync("CropCatalog.Created", null, "CropCatalogEntry", $"{type}/{added.ID}", added.Name);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPut]
        public async Task<ActionResult<bool>> CatalogUpdate(CropCatalogType type, [FromBody] CropCatalogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                return BadRequest("Name is required.");
            }
            bool updated = await catalogRepo.CatalogUpdateAsync(type, entry);
            if (!updated)
            {
                return NotFound();
            }
            await WriteAuditAsync("CropCatalog.Updated", null, "CropCatalogEntry", $"{type}/{entry.ID}", entry.Name);
            return true;
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpDelete]
        public async Task<ActionResult<bool>> CatalogDelete(CropCatalogType type, int id)
        {
            bool deleted = await catalogRepo.CatalogDeleteAsync(type, id);
            if (!deleted)
            {
                return NotFound();
            }
            await WriteAuditAsync("CropCatalog.Deleted", null, "CropCatalogEntry", $"{type}/{id}", null);
            return true;
        }
    }
}
