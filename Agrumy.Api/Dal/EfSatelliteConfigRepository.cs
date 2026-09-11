using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Security;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    internal sealed class EfSatelliteConfigRepository(AgrumyDbContext db, ISecretProtector secretProtector) : ISatelliteConfigRepository
    {
        public async Task<TenantSatelliteConfig?> SatelliteConfigGetAsync(int tenantId)
        {
            var row = await db.TenantSatelliteConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.TenantID == tenantId);
            return row == null ? null : ToDto(row);
        }

        public async Task<IList<TenantSatelliteConfig>> SatelliteConfigsGetEnabledAsync()
        {
            var rows = await db.TenantSatelliteConfigs.AsNoTracking().Where(c => c.Enabled).ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<TenantSatelliteConfig> SatelliteConfigUpsertAsync(TenantSatelliteConfig config)
        {
            var row = await db.TenantSatelliteConfigs.FirstOrDefaultAsync(c => c.TenantID == config.IDTenant);
            bool isNew = row == null;
            row ??= new TenantSatelliteConfigRow { TenantID = config.IDTenant };

            row.Provider = (int)config.Provider;
            row.ClientId = config.ClientId;
            // Blank ClientSecret means "leave the stored value alone" - the caller-facing DTO never carries the real value back, so blank can only mean "unchanged", never "clear it".
            if (!string.IsNullOrEmpty(config.ClientSecret))
            {
                row.ClientSecretEncrypted = secretProtector.Protect(config.ClientSecret);
            }
            row.PlanTier = (int)config.PlanTier;
            row.DefaultIndicesJson = System.Text.Json.JsonSerializer.Serialize(config.DefaultIndices);
            row.MaxCloudPercent = config.MaxCloudPercent;
            row.MinValidPixelPercent = config.MinValidPixelPercent;
            row.Enabled = config.Enabled;
            row.RasterRetentionDaysOverride = config.RasterRetentionDaysOverride;

            if (isNew)
            {
                db.TenantSatelliteConfigs.Add(row);
            }
            await db.SaveChangesAsync();
            return ToDto(row);
        }

        public async Task<(string? ClientId, string? ClientSecret)> SatelliteConfigCredentialsGetAsync(int tenantId)
        {
            var row = await db.TenantSatelliteConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.TenantID == tenantId);
            return row == null ? (null, null) : (row.ClientId, secretProtector.Unprotect(row.ClientSecretEncrypted));
        }

        public async Task SatelliteConfigTokenIssuedAsync(int tenantId, DateTimeOffset issuedAtUtc) =>
            await db.TenantSatelliteConfigs.Where(c => c.TenantID == tenantId).ExecuteUpdateAsync(set => set.SetProperty(c => c.LastTokenIssuedUtc, issuedAtUtc));

        public async Task SatelliteConfigQuotaSnapshotSetAsync(int tenantId, string quotaSnapshotJson, DateTimeOffset? pausedUntilUtc) =>
            await db.TenantSatelliteConfigs.Where(c => c.TenantID == tenantId).ExecuteUpdateAsync(set => set
                .SetProperty(c => c.LastQuotaSnapshotJson, quotaSnapshotJson)
                .SetProperty(c => c.QuotaPausedUntilUtc, pausedUntilUtc));

        public async Task SatelliteConfigQuotaPausedNotifiedAsync(int tenantId, DateTimeOffset? notifiedAtUtc) =>
            await db.TenantSatelliteConfigs.Where(c => c.TenantID == tenantId).ExecuteUpdateAsync(set => set.SetProperty(c => c.QuotaPausedNotifiedAtUtc, notifiedAtUtc));

        private static TenantSatelliteConfig ToDto(TenantSatelliteConfigRow r) => new()
        {
            IDTenant = r.TenantID,
            Provider = (SatelliteProvider)r.Provider,
            ClientId = r.ClientId,
            HasSecret = !string.IsNullOrEmpty(r.ClientSecretEncrypted),
            PlanTier = (SatellitePlanTier)r.PlanTier,
            DefaultIndices = string.IsNullOrEmpty(r.DefaultIndicesJson) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<SatelliteIndex>>(r.DefaultIndicesJson) ?? [],
            MaxCloudPercent = r.MaxCloudPercent,
            MinValidPixelPercent = r.MinValidPixelPercent,
            Enabled = r.Enabled,
            LastTokenIssuedUtc = r.LastTokenIssuedUtc,
            LastQuotaSnapshotJson = r.LastQuotaSnapshotJson,
            QuotaPausedUntilUtc = r.QuotaPausedUntilUtc,
            RasterRetentionDaysOverride = r.RasterRetentionDaysOverride,
            QuotaPausedNotifiedAtUtc = r.QuotaPausedNotifiedAtUtc,
        };
    }
}
