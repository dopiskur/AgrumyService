using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IDeviceOutboxRepository - raw deviceOutbox CRUD only. Uses DbExceptionClassifier directly instead of ISystemRepository.ClassifyException so this class doesn't need a facet it otherwise has no reason to depend on.
    internal sealed class EfDeviceOutboxRepository(AgrumyDbContext db) : IDeviceOutboxRepository
    {
        public async Task<bool> HasActiveOutboxItemAsync(int deviceId, CommandActionType type, DateTime utcNow)
        {
            int[] activeStatuses = [(int)CommandStatus.Pending, (int)CommandStatus.Acknowledged];
            return await db.DeviceOutboxItems.AsNoTracking().AnyAsync(c =>
                c.DeviceID == deviceId &&
                c.Type == (int)type &&
                activeStatuses.Contains(c.Status) &&
                c.ExpiresAt > utcNow);
        }

        /// Null return means the ux_deviceOutbox_device_activekey unique index rejected the insert (another request won the race, or - for ConfigChanged/HardReset - one is already pending) - the caller treats this as a dedup skip, not an error.
        public async Task<int?> AddOutboxItemAsync(int deviceId, CommandActionType type, DateTime issuedAt, DateTime expiresAt, string? payload = null)
        {
            var row = new DeviceOutboxRow
            {
                DeviceID = deviceId,
                Type = (int)type,
                Status = (int)CommandStatus.Pending,
                ActiveKey = (int)type,
                CreatedAt = issuedAt,
                ExpiresAt = expiresAt,
                Payload = payload,
            };
            db.DeviceOutboxItems.Add(row);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbExceptionClassifier.Classify(ex) == DbFailureKind.ConstraintViolation)
            {
                // Nothing was actually persisted - detach so a later SaveChangesAsync on this context doesn't retry it.
                db.Entry(row).State = EntityState.Detached;
                return null;
            }
            return row.IDDeviceOutbox;
        }

        public async Task<IList<DeviceCommand>> GetPendingOutboxItemsAsync(int deviceId)
        {
            var rows = await db.DeviceOutboxItems.AsNoTracking()
                .Where(c => c.DeviceID == deviceId && c.Status == (int)CommandStatus.Pending)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task ExpirePendingOutboxItemsAsync(int deviceId, DateTime utcNow)
        {
            // Clears ActiveKey too, same as SetOutboxItemStatusAsync's Expired branch - otherwise the freed dedup slot stays wrongly occupied.
            await db.DeviceOutboxItems
                .Where(c => c.DeviceID == deviceId && c.Status == (int)CommandStatus.Pending && c.ExpiresAt <= utcNow)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, (int)CommandStatus.Expired)
                    .SetProperty(c => c.ActiveKey, (int?)null));
        }

        public async Task<IList<DeviceCommand>> GetActiveProvisionCommandsAsync()
        {
            int[] activeStatuses = [(int)CommandStatus.Pending, (int)CommandStatus.Acknowledged];
            var rows = await db.DeviceOutboxItems.AsNoTracking()
                .Where(c => c.Type == (int)CommandActionType.ProvisionDevice && activeStatuses.Contains(c.Status))
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<DeviceCommand?> GetOutboxItemByIdAsync(int commandId)
        {
            var row = await db.DeviceOutboxItems.AsNoTracking().FirstOrDefaultAsync(c => c.IDDeviceOutbox == commandId);
            return row == null ? null : ToDto(row);
        }

        public async Task SetOutboxItemStatusAsync(int commandId, CommandStatus status, DateTime? executedAt = null)
        {
            var row = await db.DeviceOutboxItems.FirstOrDefaultAsync(c => c.IDDeviceOutbox == commandId);
            if (row == null)
            {
                return;
            }
            row.Status = (int)status;
            if (executedAt != null)
            {
                row.ExecutedAt = executedAt;
            }
            // Frees the (DeviceID, ActiveKey) unique slot the moment this row stops being active - Acknowledged keeps it set, only the two terminal states clear it.
            if (status is CommandStatus.Executed or CommandStatus.Expired)
            {
                row.ActiveKey = null;
            }
            await db.SaveChangesAsync();
        }

        public Task ConsumePendingByTypeAsync(int deviceId, CommandActionType type, DateTime executedAtUtc) =>
            db.DeviceOutboxItems
                .Where(c => c.DeviceID == deviceId && c.Type == (int)type && c.Status == (int)CommandStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, (int)CommandStatus.Executed)
                    .SetProperty(c => c.ExecutedAt, executedAtUtc)
                    .SetProperty(c => c.ActiveKey, (int?)null));

        /// Bulk-deletes terminal-status (Executed/Expired) rows older than the cutoff since deviceOutbox otherwise grows unbounded - Pending/Acknowledged rows are never touched regardless of age.
        public async Task PurgeOldOutboxItemsAsync(DateTime issuedBeforeUtc, CancellationToken ct = default)
        {
            int[] terminalStatuses = [(int)CommandStatus.Executed, (int)CommandStatus.Expired];
            await db.DeviceOutboxItems
                .Where(c => terminalStatuses.Contains(c.Status) && c.CreatedAt < issuedBeforeUtc)
                .ExecuteDeleteAsync(ct);
        }

        public async Task<IList<DeviceCommand>> GetUnpublishedPendingItemsAsync(int batchSize)
        {
            DateTime utcNow = DateTime.UtcNow;
            var rows = await db.DeviceOutboxItems.AsNoTracking()
                .Where(c => c.Status == (int)CommandStatus.Pending && c.PublishedAt == null && c.ExpiresAt > utcNow)
                .OrderBy(c => c.CreatedAt)
                .Take(batchSize)
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public Task MarkPublishedAsync(int commandId, DateTime publishedAtUtc) =>
            db.DeviceOutboxItems.Where(c => c.IDDeviceOutbox == commandId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.PublishedAt, publishedAtUtc));

        private static DeviceCommand ToDto(DeviceOutboxRow c) => new()
        {
            IDDeviceCommand = c.IDDeviceOutbox,
            DeviceID = c.DeviceID,
            ActionType = (CommandActionType)c.Type,
            Status = (CommandStatus)c.Status,
            IssuedAt = c.CreatedAt,
            ExpiresAt = c.ExpiresAt,
            ExecutedAt = c.ExecutedAt,
            Payload = c.Payload,
        };
    }
}
