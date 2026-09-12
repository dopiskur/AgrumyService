using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// TenantQuota CRUD - Global Admin only, mirroring TenantApiController's write bar; IDTenant=0 (the default/bootstrap organization) has no quota to configure, see TenantQuotaEnforcer.
    [Route("/api/TenantQuota")]
    [Authorize]
    public class TenantQuotaApiController(ITenantRepository tenantRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [HttpGet]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult<TenantQuota>> TenantQuotaGet(int idTenant)
        {
            if (idTenant == 0)
            {
                return NotFound("The default tenant has no configurable quota.");
            }
            TenantQuota? quota = await tenantRepo.TenantQuotaGetAsync(idTenant);
            return quota is null ? NotFound() : Ok(quota);
        }

        [HttpPut]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> TenantQuotaSet([FromBody] TenantQuota quota)
        {
            if (quota.IDTenant == 0)
            {
                return BadRequest("The default tenant has no configurable quota.");
            }
            await tenantRepo.TenantQuotaSetAsync(quota);
            await WriteAuditAsync("TenantQuota.Updated", quota.IDTenant, "TenantQuota", quota.IDTenant.ToString(), null);
            return Ok();
        }
    }
}
