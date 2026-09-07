namespace api.Notifications
{
    /// Fans one notification out to every configured channel.
    public interface INotificationDispatcher
    {
        Task<IReadOnlyList<ChannelOutcome>> DispatchAsync(Notification notification, CancellationToken ct = default);

        /// Fans one event out to several recipients: per-recipient channels (email, push) send once per recipient, per-event channels (webhook) send exactly once total, not once per recipient.
        Task<IReadOnlyList<ChannelOutcome>> DispatchToRecipientsAsync(string subject, string body, NotificationSeverity severity,
            IReadOnlyList<NotificationRecipient> recipients, bool containsSecret = false, CancellationToken ct = default);
    }

    public sealed record ChannelOutcome(string Channel, NotificationResult Result);

    public sealed class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IEnumerable<INotificationChannel> _channels;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(IEnumerable<INotificationChannel> channels, ILogger<NotificationDispatcher> logger)
        {
            _channels = channels;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ChannelOutcome>> DispatchAsync(Notification notification, CancellationToken ct = default)
        {
            var outcomes = new List<ChannelOutcome>();

            foreach (var channel in _channels)
            {
                outcomes.Add(await SendThroughAsync(channel, notification, ct));
            }

            if (outcomes.All(o => !o.Result.Sent))
            {
                _logger.LogWarning("Notification \"{Subject}\" was not delivered by any channel.", notification.Subject);
            }
            return outcomes;
        }

        public async Task<IReadOnlyList<ChannelOutcome>> DispatchToRecipientsAsync(string subject, string body, NotificationSeverity severity,
            IReadOnlyList<NotificationRecipient> recipients, bool containsSecret = false, CancellationToken ct = default)
        {
            if (recipients.Count == 0)
            {
                return Array.Empty<ChannelOutcome>(); // no one to notify - not even the event channels, same no-op as the old per-recipient loop with zero admin emails
            }

            var outcomes = new List<ChannelOutcome>();

            foreach (var recipient in recipients)
            {
                var notification = new Notification(subject, body, recipient, severity, containsSecret);
                foreach (var channel in _channels.Where(c => c.PerRecipient))
                {
                    outcomes.Add(await SendThroughAsync(channel, notification, ct));
                }
            }

            var eventNotification = new Notification(subject, body, new NotificationRecipient(), severity, containsSecret);
            foreach (var channel in _channels.Where(c => !c.PerRecipient))
            {
                outcomes.Add(await SendThroughAsync(channel, eventNotification, ct));
            }

            if (outcomes.Count > 0 && outcomes.All(o => !o.Result.Sent))
            {
                _logger.LogWarning("Notification \"{Subject}\" was not delivered by any channel.", subject);
            }
            return outcomes;
        }

        private async Task<ChannelOutcome> SendThroughAsync(INotificationChannel channel, Notification notification, CancellationToken ct)
        {
            if (!await channel.IsConfiguredAsync(ct))
            {
                return new ChannelOutcome(channel.Name, NotificationResult.Skipped("channel not configured"));
            }

            try
            {
                return new ChannelOutcome(channel.Name, await channel.SendAsync(notification, ct));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification channel {Channel} threw.", channel.Name);
                return new ChannelOutcome(channel.Name, NotificationResult.Failed(ex.Message));
            }
        }
    }
}
