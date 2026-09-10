using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IAuditLogRepository members - forwarded to the standalone EfAuditLogRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task AuditLogAddAsync(AuditLogEntry entry) => auditLogRepository.AuditLogAddAsync(entry);

        public Task<IReadOnlyList<AuditLogEntry>> AuditLogGetAsync(int? tenantId, int take = 200, string? actorEmail = null, string? action = null, string? targetType = null, DateTime? fromUtc = null, DateTime? toUtc = null) =>
            auditLogRepository.AuditLogGetAsync(tenantId, take, actorEmail, action, targetType, fromUtc, toUtc);
    }
}
