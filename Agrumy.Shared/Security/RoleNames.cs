namespace Agrumy.Shared.Security
{
    /// Role catalog; a user can hold several at once. These constants exist so the RoleName strings seeded by the migration and checked in [Authorize(Roles=...)]/CallerHasRole never drift apart by a typo.
    public static class RoleNames
    {
        // Base roles - exactly one per scope a user belongs to. Admin already implies every capability at that scope, so it is never combined with the grants below.
        public const string GlobalAdmin = "Global admin";
        public const string GlobalReader = "Global reader";
        public const string TenantAdmin = "Tenant admin";
        public const string TenantReader = "Tenant reader";

        // Composable grants - zero or more, layered on top of a *Reader base.
        public const string GlobalUser = "Global User";
        public const string GlobalDevice = "Global Device";
        public const string TenantUser = "Tenant User";
        public const string TenantDevice = "Tenant Device";

        // Standalone, narrow bases (like GlobalReader/TenantReader) - sensor data + server metrics only, never device configuration or user accounts (see CallerIsDataReaderOnly).
        public const string GlobalDataReader = "Global Data Reader";
        public const string TenantDataReader = "Tenant Data Reader";

        /// Manages Simulation Mode overrides without needing full Organization admin - composable, layered on top of a *Reader base like the grants above.
        public const string SimulationAdministrator = "Simulation administrator";

        public static readonly IReadOnlyList<string> All = new[]
        {
            GlobalAdmin, GlobalReader, GlobalUser, GlobalDevice,
            TenantAdmin, TenantReader, TenantUser, TenantDevice,
            GlobalDataReader, TenantDataReader, SimulationAdministrator,
        };

        /// The role picker's checkbox list - minus the five Tenant-scoped roles when TenantManagementEnabled is off, since a single-organization deployment has no use for a role that only matters across multiple organizations.
        public static IReadOnlyList<string> Selectable(bool tenantManagementEnabled) =>
            tenantManagementEnabled ? All : All.Where(r => !r.StartsWith("Tenant", StringComparison.Ordinal)).ToList();

        // Comma-separated lists for [Authorize(Roles = ...)] - any listed role passes the attribute (the coarse gate); the precise per-organization decision happens inline via ApiControllerBase's capability helpers.

        /// May manage user accounts (create/edit/delete) - organization scoping still applies inline.
        public const string UserManagers = GlobalAdmin + "," + GlobalUser + "," + TenantAdmin + "," + TenantUser;

        /// Global-tier slice of UserManagers - matches ApiControllerBase.CallerManagesUsersGlobally, the only callers allowed to migrate a user across organizations (UserApiController.UserUpdate's TenantID branch).
        public const string GlobalUserManagers = GlobalAdmin + "," + GlobalUser;

        /// May manage devices and their configs - organization scoping still applies inline.
        public const string DeviceManagers = GlobalAdmin + "," + GlobalDevice + "," + TenantAdmin + "," + TenantDevice;

        // Role GRANTING stays admin-only on purpose: an Organization User could otherwise assign themselves Organization admin.
        public const string Admins = GlobalAdmin + "," + TenantAdmin;

        /// Organization Management read access (list/view every organization) - write (create/rename) stays Global admin only, checked inline via CallerIsGlobalAdmin, since an Organization scope has no meaningful self-management of ITS OWN existence.
        public const string GlobalAdminOrReader = GlobalAdmin + "," + GlobalReader;

        /// May read server metrics (/api/metrics, /api/metrics/prometheus) - RequireRole takes individual names, not a comma-joined [Authorize] string.
        public static readonly string[] MetricsReaders = { GlobalAdmin, GlobalDataReader, TenantDataReader };

        /// May view/edit a device's Simulation Mode overrides - organization scoping still applies inline, same rule as DeviceManagers.
        public const string SimulationManagers = GlobalAdmin + "," + TenantAdmin + "," + SimulationAdministrator;

        /// GET-only widening of Admins to Global reader - write actions on the same controller keep using Admins directly.
        public const string AdminsOrGlobalReader = Admins + "," + GlobalReader;

        /// GET-only widening of DeviceManagers to Global reader - write actions on the same controller keep using DeviceManagers directly.
        public const string DeviceManagersOrGlobalReader = DeviceManagers + "," + GlobalReader;

        /// GET-only widening of SimulationManagers to Global reader - write actions on the same controller keep using SimulationManagers directly.
        public const string SimulationManagersOrGlobalReader = SimulationManagers + "," + GlobalReader;

        /// GET-only widening of UserManagers to Global reader - write actions on the same controller keep using UserManagers directly.
        public const string UserManagersOrGlobalReader = UserManagers + "," + GlobalReader;

        /// Every role except the device-scoped grants (GlobalDevice/TenantDevice) - a service account holding one of those exists to manage devices, not to bulk-read sensor data, so it must not pass the OData/Power BI feed.
        public const string SensorDataReaders = GlobalAdmin + "," + GlobalReader + "," + GlobalUser + "," + TenantAdmin + "," + TenantReader + "," + TenantUser + "," + GlobalDataReader + "," + TenantDataReader + "," + SimulationAdministrator;
    }
}
