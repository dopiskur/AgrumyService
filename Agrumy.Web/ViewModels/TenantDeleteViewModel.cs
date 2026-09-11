namespace Agrumy.Web.ViewModels
{
    /// Delete.cshtml's model - UserCount lets the confirm page warn about how many users the "delete users too" checkbox would take with it; DeviceCount is shown even though a non-zero count already blocks the delete server-side, so the admin sees why before submitting.
    public class TenantDeleteViewModel
    {
        public int IDTenant { get; set; }
        public string? TenantName { get; set; }
        public int UserCount { get; set; }
        public int DeviceCount { get; set; }
        public bool DeleteUsers { get; set; }
    }
}
