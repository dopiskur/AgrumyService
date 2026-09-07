using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Agrumy.Api.Controllers.API
{
    /// Shared base for the JSON API controllers - cache injection and caller identity off the JWT; <see cref="Agrumy.Api.Filters.DbExceptionFilter"/> turns data-access exceptions into responses so actions don't catch them individually. Only takes the two facets WriteAuditAsync itself needs - controllers inject whichever wider/narrower repository facets their own actions call, not through this base.
    [ApiController]
    [ApiVersion("1.0")]
    public abstract class ApiControllerBase : ControllerBase
    {
        private readonly IUserRepository userRepo;
        private readonly IAuditLogRepository auditLogRepo;
        protected ICache Cache { get; }

        protected ApiControllerBase(IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache)
        {
            this.userRepo = userRepo;
            this.auditLogRepo = auditLogRepo;
            Cache = cache;
        }

        /// TenantID claim set at login (JwtTokenProvider.CreateToken), or null if absent.
        protected int? CallerTenantId
        {
            get
            {
                var claim = (User.Identity as ClaimsIdentity)?.FindFirst("TenantID");
                return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
            }
        }

        /// Every role claim on the caller's token.
        protected IEnumerable<string> CallerRoles =>
            (User.Identity as ClaimsIdentity)?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();

        /// True if the caller holds this exact role name, among possibly several.
        protected bool CallerHasRole(string roleName) => CallerRoles.Contains(roleName);

        protected bool CallerIsGlobalAdmin => CallerHasRole(RoleNames.GlobalAdmin);

        protected bool CallerManagesUsersGlobally =>
            CallerIsGlobalAdmin || CallerHasRole(RoleNames.GlobalUser);

        /// May the caller create/edit/delete users belonging to <paramref name="targetTenantId"/>.
        protected bool CallerManagesUsers(int? targetTenantId) =>
            CallerManagesUsersGlobally ||
            ((CallerHasRole(RoleNames.TenantAdmin) || CallerHasRole(RoleNames.TenantUser))
             && targetTenantId == CallerTenantId);

        // Numeric privilege per role - CallerOutranksTarget requires callerRank strictly greater than targetRank, so two peers holding the same role (e.g. two Tenant Users) never outrank each other.
        private static readonly Dictionary<string, int> RoleRanks = new()
        {
            [RoleNames.GlobalAdmin] = 100,
            [RoleNames.GlobalUser] = 90,
            [RoleNames.GlobalDevice] = 90,
            [RoleNames.GlobalReader] = 80,
            [RoleNames.GlobalDataReader] = 80,
            [RoleNames.TenantAdmin] = 50,
            [RoleNames.SimulationAdministrator] = 25,
            [RoleNames.TenantUser] = 20,
            [RoleNames.TenantDevice] = 20,
            [RoleNames.TenantDataReader] = 15,
            [RoleNames.TenantReader] = 10,
        };

        private static int RoleRank(IEnumerable<string> roleNames) =>
            roleNames.Select(r => RoleRanks.GetValueOrDefault(r, 0)).DefaultIfEmpty(0).Max();

        /// Beyond CallerManagesUsers' tenant check: may the caller act on a user holding <paramref name="targetRoleNames"/>, given relative privilege - Global admin outranks everyone including a Global admin peer; a Global User grant outranks everyone except a Global admin, including a Global User peer; Tenant admin outranks everyone in-tenant including a Tenant admin peer; below that, a strictly-greater numeric rank is required, so a Tenant User (or other composable grant) can never act on a peer holding the same or a higher rank.
        protected bool CallerOutranksTarget(IEnumerable<string> targetRoleNames)
        {
            ICollection<string> targetRoles = targetRoleNames as ICollection<string> ?? targetRoleNames.ToList();
            if (targetRoles.Contains(RoleNames.GlobalAdmin))
            {
                return CallerIsGlobalAdmin;
            }
            if (CallerManagesUsersGlobally)
            {
                return true;
            }
            if (CallerHasRole(RoleNames.TenantAdmin))
            {
                return true;
            }
            return RoleRank(CallerRoles) > RoleRank(targetRoles);
        }

        protected bool CallerManagesDevicesGlobally =>
            CallerIsGlobalAdmin || CallerHasRole(RoleNames.GlobalDevice);

        /// May the caller modify/delete devices belonging to <paramref name="targetTenantId"/>.
        protected bool CallerManagesDevices(int? targetTenantId) =>
            CallerManagesDevicesGlobally ||
            ((CallerHasRole(RoleNames.TenantAdmin) || CallerHasRole(RoleNames.TenantDevice))
             && targetTenantId == CallerTenantId);

        // Reads: managing implies reading; Global reader reads everything everywhere but writes nothing.
        protected bool CallerReadsUsersGlobally =>
            CallerManagesUsersGlobally || CallerHasRole(RoleNames.GlobalReader);

        protected bool CallerReadsDevicesGlobally =>
            CallerManagesDevicesGlobally || CallerHasRole(RoleNames.GlobalReader);

        /// True only when a Data Reader grant is the caller's SOLE access - device configuration/rules and user accounts stay hidden from that narrow, sensor-data-and-metrics-only role, but never from anyone who also holds a broader role.
        protected bool CallerIsDataReaderOnly =>
            (CallerHasRole(RoleNames.GlobalDataReader) || CallerHasRole(RoleNames.TenantDataReader))
            && !CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader) && !CallerManagesDevicesGlobally
            && !CallerHasRole(RoleNames.TenantAdmin) && !CallerHasRole(RoleNames.TenantReader) && !CallerHasRole(RoleNames.TenantDevice)
            && !CallerManagesUsersGlobally && !CallerHasRole(RoleNames.TenantUser);

        /// Shared body behind each Device-domain controller's per-entity EnsureOwned* helper: 404 on missing, 403 on tenant mismatch unless the caller's role crosses tenants (CallerManagesDevicesGlobally on a write, the wider CallerReadsDevicesGlobally on a read).
        protected async Task<(T? Entity, ActionResult? Error)> EnsureOwnedDeviceEntityAsync<T>(Func<Task<T?>> lookup, Func<T, int?> tenantIdOf, string ownerLabel, bool forWrite) where T : class
        {
            T? entity = await lookup();
            if (entity is null)
            {
                return (null, NotFound());
            }
            bool crossTenantAllowed = forWrite ? CallerManagesDevicesGlobally : CallerReadsDevicesGlobally;
            if (tenantIdOf(entity) != CallerTenantId && !crossTenantAllowed)
            {
                return (entity, StatusCode(403, $"{ownerLabel} belongs to a different tenant"));
            }
            return (entity, null);
        }

        /// Looks up the caller's IDUser by their JWT email rather than trusting a claim, since the token carries no user-id claim.
        protected async Task WriteAuditAsync(string action, int? targetTenantId, string targetType, string targetId, string? details)
        {
            string? actorEmail = User.Identity?.Name;
            User? actor = string.IsNullOrEmpty(actorEmail) ? null : await userRepo.UserGetAsync(null, actorEmail, null);
            await auditLogRepo.AuditLogAddAsync(new AuditLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                TenantID = targetTenantId,
                ActorUserID = actor?.IDUser,
                ActorEmail = actorEmail,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Details = details,
            });
        }
    }
}
