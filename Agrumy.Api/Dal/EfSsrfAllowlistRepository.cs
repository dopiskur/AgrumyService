using Agrumy.Api.Dal.Interface;
using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// ISsrfAllowlistRepository - a leaf facet, no dependency on any other domain.
    internal sealed class EfSsrfAllowlistRepository(AgrumyDbContext db) : ISsrfAllowlistRepository
    {
        public async Task<IReadOnlyList<SsrfAllowlistEntry>> FirmwareAllowlistGetAllAsync() =>
            await db.FirmwareSsrfAllowlistEntries.AsNoTracking()
                .Select(r => new SsrfAllowlistEntry { Id = r.IDFirmwareSsrfAllowlistEntry, Pattern = r.Pattern, AllowPrivateNetwork = r.AllowPrivateNetwork, AllowInsecureHttp = r.AllowInsecureHttp })
                .ToListAsync();

        public async Task<SsrfAllowlistEntry> FirmwareAllowlistAddAsync(SsrfAllowlistEntry entry)
        {
            var row = new FirmwareSsrfAllowlistEntryRow
            {
                Pattern = entry.Pattern,
                AllowPrivateNetwork = entry.AllowPrivateNetwork,
                AllowInsecureHttp = entry.AllowInsecureHttp,
            };
            db.FirmwareSsrfAllowlistEntries.Add(row);
            await db.SaveChangesAsync();
            return new SsrfAllowlistEntry { Id = row.IDFirmwareSsrfAllowlistEntry, Pattern = row.Pattern, AllowPrivateNetwork = row.AllowPrivateNetwork, AllowInsecureHttp = row.AllowInsecureHttp };
        }

        public async Task FirmwareAllowlistDeleteAsync(int id)
        {
            await db.FirmwareSsrfAllowlistEntries.Where(x => x.IDFirmwareSsrfAllowlistEntry == id).ExecuteDeleteAsync();
        }

        public async Task<IReadOnlyList<SsrfAllowlistEntry>> WebhookAllowlistGetAllAsync() =>
            await db.WebhookSsrfAllowlistEntries.AsNoTracking()
                .Select(r => new SsrfAllowlistEntry { Id = r.IDWebhookSsrfAllowlistEntry, Pattern = r.Pattern, AllowPrivateNetwork = r.AllowPrivateNetwork, AllowInsecureHttp = r.AllowInsecureHttp })
                .ToListAsync();

        public async Task<SsrfAllowlistEntry> WebhookAllowlistAddAsync(SsrfAllowlistEntry entry)
        {
            var row = new WebhookSsrfAllowlistEntryRow
            {
                Pattern = entry.Pattern,
                AllowPrivateNetwork = entry.AllowPrivateNetwork,
                AllowInsecureHttp = entry.AllowInsecureHttp,
            };
            db.WebhookSsrfAllowlistEntries.Add(row);
            await db.SaveChangesAsync();
            return new SsrfAllowlistEntry { Id = row.IDWebhookSsrfAllowlistEntry, Pattern = row.Pattern, AllowPrivateNetwork = row.AllowPrivateNetwork, AllowInsecureHttp = row.AllowInsecureHttp };
        }

        public async Task WebhookAllowlistDeleteAsync(int id)
        {
            await db.WebhookSsrfAllowlistEntries.Where(x => x.IDWebhookSsrfAllowlistEntry == id).ExecuteDeleteAsync();
        }
    }
}
