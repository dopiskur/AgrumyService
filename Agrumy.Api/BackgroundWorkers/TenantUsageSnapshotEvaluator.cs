using Agrumy.Api.Dal.Interface;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Records one usage snapshot per organization per day, kept separate from TenantUsageSnapshotBackgroundService so it is directly unit-testable with a mocked repository.
    public sealed class TenantUsageSnapshotEvaluator(ITenantRepository tenantRepo)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            DateTimeOffset today = DateTimeOffset.UtcNow.Date;
            var tenants = await tenantRepo.TenantsGetAllAsync();

            foreach (var tenant in tenants)
            {
                ct.ThrowIfCancellationRequested();
                if (tenant.IDTenant is int idTenant)
                {
                    await tenantRepo.TenantUsageSnapshotRecordAsync(idTenant, today);
                }
            }
        }
    }
}
