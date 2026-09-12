namespace Agrumy.Shared.Models
{
    /// Body of the RecycleBinApiController mark-for-purge endpoints and DataMaintenanceApiController.PurgeOrphaned - ConfirmationPhrase must match RequiredPhrase, checked server-side.
    public class RecycleBinPurgeRequest
    {
        public const string RequiredPhrase = "PURGE";
        public string? ConfirmationPhrase { get; set; }
    }

    /// Result of POST /api/RecycleBin/Empty - counts marked for purge, not yet actually removed (that happens later, when the purge cycle reaps them).
    public class RecycleBinEmptyResult
    {
        public int DevicesMarked { get; set; }
        public int FarmsMarked { get; set; }
    }
}
