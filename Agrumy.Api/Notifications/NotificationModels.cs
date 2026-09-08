namespace Agrumy.Api.Notifications
{
    public enum NotificationSeverity
    {
        Info,
        Warning,
        Critical,
    }

    /// Where a notification goes. A channel uses whichever fields it understands.
    public sealed record NotificationRecipient(
        string? Email = null,
        IReadOnlyList<string>? PushTokens = null);

    /// Per-recipient channel names a user can individually opt in/out of (matches EmailNotificationChannel/FcmPushNotificationChannel's own Name) - Webhook is per-EVENT (INotificationChannel.PerRecipient false), so it has no per-user concept and is deliberately absent here.
    public static class NotificationChannels
    {
        public const string Email = "email";
        public const string PushFcm = "push-fcm";
        public static readonly IReadOnlyList<string> PerRecipient = [Email, PushFcm];
    }

    /// ContainsSecret marks a notification whose Body carries a live credential (e.g. an activation-link token) - WebhookNotificationChannel refuses to forward those to its shared, operator-configured endpoint.
    public sealed record Notification(
        string Subject,
        string Body,
        NotificationRecipient Recipient,
        NotificationSeverity Severity = NotificationSeverity.Warning,
        bool ContainsSecret = false);

    /// Outcome of one channel handling one notification; <see cref="Sent"/> false covers both "not applicable" (<see cref="Skipped"/>) and "tried and failed" (<see cref="Failed"/>).
    public sealed record NotificationResult(bool Sent, bool Attempted, string? Detail)
    {
        public static NotificationResult Ok(string? detail = null) => new(true, true, detail);
        public static NotificationResult Skipped(string reason) => new(false, false, reason);
        public static NotificationResult Failed(string reason) => new(false, true, reason);
    }
}
