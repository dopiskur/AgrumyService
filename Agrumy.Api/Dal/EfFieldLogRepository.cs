using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    internal sealed class EfFieldLogRepository(AgrumyDbContext db) : IFieldLogRepository
    {
        public async Task<IList<FieldLogEntry>> FieldLogEntriesGetAsync(int? sowingID, int? farmParcelZoneID, int? zonePlantingID, int? deviceFarmUnitZoneID)
        {
            IQueryable<FieldLogEntryRow> q = db.FieldLogEntries.AsNoTracking();
            if (sowingID != null) q = q.Where(e => e.SowingID == sowingID);
            if (farmParcelZoneID != null) q = q.Where(e => e.FarmParcelZoneID == farmParcelZoneID);
            if (zonePlantingID != null) q = q.Where(e => e.ZonePlantingID == zonePlantingID);
            if (deviceFarmUnitZoneID != null) q = q.Where(e => e.DeviceFarmUnitZoneID == deviceFarmUnitZoneID);
            var rows = await q.OrderByDescending(e => e.DateUtc).ToListAsync();
            return rows.Select(ToDtoEntry).ToList();
        }

        public async Task<FieldLogEntry?> FieldLogEntryGetByIdAsync(int idFieldLogEntry)
        {
            var row = await db.FieldLogEntries.AsNoTracking().FirstOrDefaultAsync(e => e.IDFieldLogEntry == idFieldLogEntry);
            return row == null ? null : ToDtoEntry(row);
        }

        public async Task<FieldLogEntry> FieldLogEntryAddAsync(FieldLogEntry entry)
        {
            var row = new FieldLogEntryRow
            {
                TenantID = entry.TenantID,
                SowingID = entry.SowingID,
                FarmParcelZoneID = entry.FarmParcelZoneID,
                ZonePlantingID = entry.ZonePlantingID,
                DeviceFarmUnitZoneID = entry.DeviceFarmUnitZoneID,
                EntryType = (int)entry.EntryType,
                DateUtc = entry.DateUtc == default ? DateTimeOffset.UtcNow : entry.DateUtc,
                CreatedByUserID = entry.CreatedByUserID,
                Note = entry.Note,
                PayloadJson = entry.PayloadJson,
                IsClosingEntry = entry.IsClosingEntry,
            };
            db.FieldLogEntries.Add(row);
            await db.SaveChangesAsync();
            return ToDtoEntry(row);
        }

        public async Task FieldLogEntryDeleteAsync(int idFieldLogEntry)
        {
            await db.FieldLogAttachments.Where(a => a.FieldLogEntryID == idFieldLogEntry).ExecuteDeleteAsync();
            await db.FieldLogEntries.Where(e => e.IDFieldLogEntry == idFieldLogEntry).ExecuteDeleteAsync();
        }

        public async Task<IList<FieldLogAttachment>> FieldLogAttachmentsGetAsync(int idFieldLogEntry)
        {
            var rows = await db.FieldLogAttachments.AsNoTracking().Where(a => a.FieldLogEntryID == idFieldLogEntry).ToListAsync();
            return rows.Select(ToDtoAttachment).ToList();
        }

        public async Task<FieldLogAttachment> FieldLogAttachmentAddAsync(FieldLogAttachment attachment)
        {
            var row = new FieldLogAttachmentRow
            {
                FieldLogEntryID = attachment.FieldLogEntryID,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                StoragePath = attachment.StoragePath,
                SizeBytes = attachment.SizeBytes,
            };
            db.FieldLogAttachments.Add(row);
            await db.SaveChangesAsync();
            return ToDtoAttachment(row);
        }

        public async Task FieldLogAttachmentDeleteAsync(int idFieldLogAttachment) =>
            await db.FieldLogAttachments.Where(a => a.IDFieldLogAttachment == idFieldLogAttachment).ExecuteDeleteAsync();

        public async Task<IList<HarvestResult>> HarvestResultsGetAsync(int? sowingID, int? zonePlantingID)
        {
            IQueryable<HarvestResultRow> q = db.HarvestResults.AsNoTracking();
            if (sowingID != null) q = q.Where(h => h.SowingID == sowingID);
            if (zonePlantingID != null) q = q.Where(h => h.ZonePlantingID == zonePlantingID);
            var rows = await q.OrderByDescending(h => h.DateUtc).ToListAsync();
            return rows.Select(ToDtoHarvest).ToList();
        }

        public async Task<HarvestResult> HarvestResultAddAsync(HarvestResult result)
        {
            var row = new HarvestResultRow
            {
                SowingID = result.SowingID,
                ZonePlantingID = result.ZonePlantingID,
                FarmParcelZoneID = result.FarmParcelZoneID,
                DateUtc = result.DateUtc == default ? DateTimeOffset.UtcNow : result.DateUtc,
                YieldKg = result.YieldKg,
                MoisturePercent = result.MoisturePercent,
                QualityGrade = result.QualityGrade,
                LossesKg = result.LossesKg,
                MetricsJson = result.MetricsJson,
                Note = result.Note,
            };
            db.HarvestResults.Add(row);
            await db.SaveChangesAsync();
            return ToDtoHarvest(row);
        }

        private static FieldLogEntry ToDtoEntry(FieldLogEntryRow e) => new()
        {
            IDFieldLogEntry = e.IDFieldLogEntry,
            TenantID = e.TenantID,
            SowingID = e.SowingID,
            FarmParcelZoneID = e.FarmParcelZoneID,
            ZonePlantingID = e.ZonePlantingID,
            DeviceFarmUnitZoneID = e.DeviceFarmUnitZoneID,
            EntryType = (EntryType)e.EntryType,
            DateUtc = e.DateUtc,
            CreatedByUserID = e.CreatedByUserID,
            Note = e.Note,
            PayloadJson = e.PayloadJson,
            IsClosingEntry = e.IsClosingEntry,
        };

        private static FieldLogAttachment ToDtoAttachment(FieldLogAttachmentRow a) => new()
        {
            IDFieldLogAttachment = a.IDFieldLogAttachment,
            FieldLogEntryID = a.FieldLogEntryID,
            FileName = a.FileName,
            ContentType = a.ContentType,
            StoragePath = a.StoragePath,
            SizeBytes = a.SizeBytes,
        };

        private static HarvestResult ToDtoHarvest(HarvestResultRow h) => new()
        {
            IDHarvestResult = h.IDHarvestResult,
            SowingID = h.SowingID,
            ZonePlantingID = h.ZonePlantingID,
            FarmParcelZoneID = h.FarmParcelZoneID,
            DateUtc = h.DateUtc,
            YieldKg = h.YieldKg,
            MoisturePercent = h.MoisturePercent,
            QualityGrade = h.QualityGrade,
            LossesKg = h.LossesKg,
            MetricsJson = h.MetricsJson,
            Note = h.Note,
        };
    }
}
