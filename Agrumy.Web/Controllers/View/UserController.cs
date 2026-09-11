using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    [Authorize]
    public class UserController(IApi api) : Controller
    {
        [Authorize(Roles = RoleNames.UserManagersOrGlobalReader)]
        public async Task<ActionResult> Index()
        {
            IEnumerable<User> users = await api.UsersGet();
            ViewBag.TenantNames = await ResolveTenantNamesAsync(users.Select(u => u.TenantID));
            return View(users);
        }

        [Authorize(Roles = RoleNames.UserManagersOrGlobalReader)]
        public async Task<ActionResult> Details(int? idUser)
        {
            User user = await api.UserGet(idUser);
            ViewBag.TenantName = await ResolveTenantNameAsync(user.TenantID);
            ViewBag.AssignedRoles = await TryGetAssignedRolesAsync(idUser!.Value);
            return View(user);
        }

        /// null for an "Unassigned" user (TenantID cleared by TenantController.DeleteConfirm) - never calls the API for that case. ApiException swallowed - a Tenant User has no cross-tenant read here, so a caller viewing another tenant's user (Global reader path) just shows nothing rather than an error page.
        private async Task<string?> ResolveTenantNameAsync(int? tenantId)
        {
            if (tenantId is not int id) { return null; }
            try { return (await api.TenantGet(id)).TenantName; }
            catch (ApiException) { return null; }
        }

        /// One TenantGet per distinct id actually present in the list - a Global admin/reader's Index (many tenants) pays for it, a Tenant-scoped viewer's Index (their own tenant only) makes at most one call.
        private async Task<Dictionary<int, string?>> ResolveTenantNamesAsync(IEnumerable<int?> tenantIds)
        {
            var result = new Dictionary<int, string?>();
            foreach (int id in tenantIds.Where(id => id != null).Select(id => id!.Value).Distinct())
            {
                result[id] = await ResolveTenantNameAsync(id);
            }
            return result;
        }

        /// UserApiController.UserRolesGet is UserManagers-only server-side, narrower than this controller's UserManagersOrGlobalReader (a Global reader can view Details but not roles) - swallow the 403 rather than fail the whole page.
        private async Task<IEnumerable<string>?> TryGetAssignedRolesAsync(int idUser)
        {
            try { return await api.UserRolesGet(idUser); }
            catch (ApiException) { return null; }
        }

        [Authorize(Roles = RoleNames.UserManagers)]
        public ActionResult Create() => View(new UserView());

        [Authorize(Roles = RoleNames.UserManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(UserView userView)
        {
            if (!ModelState.IsValid)
            {
                return View(userView);
            }

            await api.UserAdd(userView.UserAdd!);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RoleNames.UserManagersOrGlobalReader)]
        public async Task<ActionResult> Edit(int? idUser)
        {
            var user = await api.UserGet(idUser);
            var assignedRoles = await api.UserRolesGet(idUser!.Value);
            ViewBag.TenantName = await ResolveTenantNameAsync(user.TenantID);
            ViewBag.CanMigrateTenant = User.IsInRole(RoleNames.GlobalAdmin) || User.IsInRole(RoleNames.GlobalUser);
            return View(new UserView
            {
                UserUpdate = new UserUpdate
                {
                    IDUser = user.IDUser,
                    TenantID = user.TenantID,
                    Email = user.Email,
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Phone = user.Phone,
                    PhoneEnabled = user.PhoneEnabled ?? true,
                    RoleNames = assignedRoles.ToList(),
                    Enabled = user.Enabled ?? false,
                },
            });
        }

        [Authorize(Roles = RoleNames.UserManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(UserView userView)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.TenantName = await ResolveTenantNameAsync(userView.UserUpdate!.TenantID);
                ViewBag.CanMigrateTenant = User.IsInRole(RoleNames.GlobalAdmin) || User.IsInRole(RoleNames.GlobalUser);
                return View(userView);
            }

            await api.UserUpdate(userView.UserUpdate!);
            // PRG: redirect so a refresh re-fetches Details instead of re-submitting the update.
            return RedirectToAction(nameof(Details), new { idUser = userView.UserUpdate!.IDUser });
        }

        /// Global-tier only (RoleNames.GlobalUserManagers), same bar as UserApiController.UserUpdate's TenantID branch (CallerManagesUsersGlobally) - a Tenant admin can manage users but never move them to a tenant they don't administer.
        [Authorize(Roles = RoleNames.GlobalUserManagers)]
        public async Task<ActionResult> MigrateTenant(int idUser)
        {
            User user = await api.UserGet(idUser);
            var otherTenants = (await api.TenantsGet()).Where(t => t.IDTenant != user.TenantID).ToList();
            return View(new UserMigrateTenantViewModel
            {
                IDUser = idUser,
                Email = user.Email,
                CurrentTenantName = await ResolveTenantNameAsync(user.TenantID),
                Tenants = otherTenants,
            });
        }

        [Authorize(Roles = RoleNames.GlobalUserManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> MigrateTenant(UserMigrateTenantViewModel value)
        {
            try
            {
                await api.UserUpdate(new UserUpdate { IDUser = value.IDUser, TenantID = value.NewTenantID });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Body);
                User user = await api.UserGet(value.IDUser);
                value.Email = user.Email;
                value.CurrentTenantName = await ResolveTenantNameAsync(user.TenantID);
                value.Tenants = (await api.TenantsGet()).Where(t => t.IDTenant != user.TenantID).ToList();
                return View(value);
            }
            return RedirectToAction(nameof(Details), new { idUser = value.IDUser });
        }

        [Authorize(Roles = RoleNames.UserManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleEnabled(int idUser, bool enabled)
        {
            try
            {
                await api.UserUpdate(new UserUpdate { IDUser = idUser, Enabled = enabled });
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RoleNames.Admins)]
        public async Task<ActionResult> Roles(int? idUser)
        {
            var user = await api.UserGet(idUser);
            var assigned = await api.UserRolesGet(idUser!.Value);
            return View(new UserRolesViewModel
            {
                IDUser = idUser.Value,
                Email = user.Email,
                AllRoles = RoleNames.All,
                AssignedRoles = assigned,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNames.Admins)]
        public async Task<ActionResult> Roles(UserRolesViewModel value)
        {
            try
            {
                await api.UserRolesSet(new UserRolesUpdate { IDUser = value.IDUser, RoleNames = value.AssignedRoles.ToList() });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Body);
                value.AllRoles = RoleNames.All;
                return View(value);
            }
            return RedirectToAction(nameof(Details), new { idUser = value.IDUser });
        }

        [Authorize(Roles = RoleNames.UserManagers)]
        public async Task<ActionResult> Delete(int? idUser) =>
            View(await api.UserGet(idUser));

        [Authorize(Roles = RoleNames.UserManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirm(int? idUser)
        {
            await api.UserDelete(idUser);
            return RedirectToAction(nameof(Index));
        }
    }
}
