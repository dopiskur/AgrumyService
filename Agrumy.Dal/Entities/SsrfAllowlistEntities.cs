namespace Agrumy.Dal.Entities
{
    /// HttpFirmwareFetcher's own SsrfGuard exceptions, kept in a dedicated table separate from WebhookSsrfAllowlistEntryRow so whitelisting a firmware repo never widens what the webhook channel can reach.
    public class FirmwareSsrfAllowlistEntryRow
    {
        public int IDFirmwareSsrfAllowlistEntry { get; set; }
        public string Pattern { get; set; } = "";
        public bool AllowPrivateNetwork { get; set; }
        public bool AllowInsecureHttp { get; set; }
    }

    /// WebhookNotificationChannel's own SsrfGuard exceptions, separate from FirmwareSsrfAllowlistEntryRow, same reasoning.
    public class WebhookSsrfAllowlistEntryRow
    {
        public int IDWebhookSsrfAllowlistEntry { get; set; }
        public string Pattern { get; set; } = "";
        public bool AllowPrivateNetwork { get; set; }
        public bool AllowInsecureHttp { get; set; }
    }
}
