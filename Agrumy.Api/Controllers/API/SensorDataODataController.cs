using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Agrumy.Api.Controllers.API
{
    /// Power BI/OData feed - query-only, no EDM model/route-component registration (see Program.cs's AddOData call): [EnableQuery] composes $filter/$select/$orderby/$top/$count directly onto the IQueryable this action returns, still translated to SQL by EF Core. RBAC/TenantID are enforced BEFORE that composition (SensorDataODataQueryable is called with the caller's own organization), so no OData query string can widen the result past it. [ApiVersionNeutral] since a Power BI feed URL has to stay stable regardless of the JSON API's own version - Asp.Versioning's analyzer otherwise requires OData to go through its own versioned-routing package, which this simple query-only feed doesn't need.
    [Route("api/odata/SensorData")]
    [ApiVersionNeutral]
    [Authorize(Roles = RoleNames.SensorDataReaders)]
    public class SensorDataODataController(ISensorDataRepository sensorDataRepo, IServerConfigRepository serverConfigRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache)
        : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [HttpGet]
        [EnableQuery]
        public async Task<ActionResult<IQueryable<SensorDataODataEntry>>> Get()
        {
            if (!(await serverConfigRepo.ServerConfigGetAsync(1)).ODataEnabled)
            {
                return NotFound();
            }
            if (CallerTenantId is not int tenantId)
            {
                return Forbid();
            }
            return Ok(sensorDataRepo.SensorDataODataQueryable(tenantId));
        }
    }
}
