namespace Agrumy.Shared.Models
{
    /// Allowed cutoffs for data maintenance actions, validated server-side so a hand-crafted API call can't pass an arbitrary value.
    public static class DataMaintenanceThresholds
    {
        public static readonly IReadOnlyList<int> AllowedDays = [90, 180, 365, 730, 1825, 3650];
    }

    /// Body of POST /api/DataMaintenance/Optimize.
    public class DataMaintenanceRequest
    {
        public int OlderThanDays { get; set; }
    }

    /// Body of POST /api/DataMaintenance/Purge; ConfirmationPhrase must match RequiredPhrase (checked server-side, not just by the Web form).
    public class DataPurgeRequest
    {
        public const string RequiredPhrase = "PURGE";

        public int OlderThanDays { get; set; }
        public string? ConfirmationPhrase { get; set; }
    }
}
