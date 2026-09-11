namespace Agrumy.Shared.Models
{
    /// One entry per alert-generating background worker - matches the four evaluators that currently blanket-notify every tenant admin (OfflineAlertEvaluator, LowBatteryAlertEvaluator, TankRefillAlertEvaluator, RuleNotificationEvaluator).
    public enum NotificationEventType
    {
        Offline = 1,
        LowBattery = 2,
        TankRefill = 3,
        RuleTriggered = 4,
        Frost = 5,
        SatelliteQuotaPaused = 6,
        SatelliteSyncFailed = 7,
        SatelliteBackfillCompleted = 8,
    }

    /// Opt-OUT model: absence of a row for (UserID, EventType, Channel) means enabled, matching every admin's current behavior before this preference existed - a row only ever needs to exist to record a user turning something off. Channel is a per-RECIPIENT channel name (matches INotificationChannel.Name for Email/Push) - a per-event channel like Webhook has no per-user concept and ignores this table entirely.
    public class UserNotificationPreference
    {
        public int UserID { get; set; }
        public NotificationEventType EventType { get; set; }
        public string Channel { get; set; } = "";
        public bool Enabled { get; set; }
    }
}
