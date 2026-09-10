using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Firmware and Webhook each get their own independent SsrfGuard allowlist, never shared.
    public interface ISsrfAllowlistRepository
    {
        Task<IReadOnlyList<SsrfAllowlistEntry>> FirmwareAllowlistGetAllAsync();
        Task<SsrfAllowlistEntry> FirmwareAllowlistAddAsync(SsrfAllowlistEntry entry);
        Task FirmwareAllowlistDeleteAsync(int id);

        Task<IReadOnlyList<SsrfAllowlistEntry>> WebhookAllowlistGetAllAsync();
        Task<SsrfAllowlistEntry> WebhookAllowlistAddAsync(SsrfAllowlistEntry entry);
        Task WebhookAllowlistDeleteAsync(int id);
    }
}
