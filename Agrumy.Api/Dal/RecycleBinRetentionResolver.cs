namespace Agrumy.Api.Dal
{
    /// Shared between EfDeviceRepository and EfDeviceFarmUnitRepository's own retention-marking passes - a governing TenantQuota's RecycleBinRetentionDays replaces the tenant's own self-configured override entirely, not merely caps it.
    internal static class RecycleBinRetentionResolver
    {
        public static int EffectiveRecycleBinRetentionDays(
            int? tenantId,
            IReadOnlyDictionary<int, int> tenantQuotaRetentionDays,
            IReadOnlyDictionary<int, int?> tenantOwnRetentionDays,
            int serverDefaultRetentionDays)
        {
            if (tenantId is int id)
            {
                if (tenantQuotaRetentionDays.TryGetValue(id, out int quotaDays))
                {
                    return quotaDays;
                }
                if (tenantOwnRetentionDays.TryGetValue(id, out int? tenantOverride) && tenantOverride != null)
                {
                    return tenantOverride.Value;
                }
            }
            return serverDefaultRetentionDays;
        }
    }
}
