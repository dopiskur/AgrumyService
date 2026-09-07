namespace Agrumy.Api.Notifications
{
    /// One delivery mechanism for an alert (email, push, ...); <see cref="NotificationDispatcher"/> fans out to every registered channel whose <see cref="IsConfiguredAsync"/> is true.
    public interface INotificationChannel
    {
        string Name { get; }

        /// True for channels addressed to one recipient (email, push) - sent once per recipient. False for channels addressed to the event itself (webhook) - sent once total by DispatchToRecipientsAsync, not once per admin.
        bool PerRecipient { get; }

        /// True when this channel has enough config to attempt a send - a disabled/unconfigured channel returns false and is skipped, never treated as a failure. Async because EmailNotificationChannel's config now lives in the DB-backed ServerConfig, not a bound options snapshot.
        Task<bool> IsConfiguredAsync(CancellationToken ct = default);

        Task<NotificationResult> SendAsync(Notification notification, CancellationToken ct = default);
    }
}
