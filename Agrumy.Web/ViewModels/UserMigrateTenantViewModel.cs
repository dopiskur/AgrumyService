using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// UserController.MigrateTenant's model - Organizations is always "every organization except the user's current one", so the dropdown can never accidentally submit a same-organization no-op.
    public class UserMigrateTenantViewModel
    {
        public int IDUser { get; set; }
        public string? Email { get; set; }
        public string? CurrentTenantName { get; set; }
        public int? NewTenantID { get; set; }
        public List<Tenant> Tenants { get; set; } = new();
    }
}
