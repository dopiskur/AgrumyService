using System.Security.Claims;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Web.Security;
using Agrumy.Shared.Utils;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Agrumy.Web.Controllers.View
{
    [Authorize]
    public class ProfileController(IApi api) : Controller
    {
        public async Task<ActionResult> Index()
        {
            return View(await BuildViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(ProfileViewModel value)
        {
            if (!ModelState.IsValid)
            {
                return View(await RestoreDisplayFieldsAsync(value));
            }

            // UIMode now moves exclusively through ToggleUIMode (header button) - this form no longer posts it, so keep the existing value instead of letting it bind to the default.
            value.Profile.UIMode = User.GetUIMode();

            try
            {
                await api.UserProfileSet(value.Profile);
            }
            catch (ApiException ex)
            {
                // Field-keyed so the error renders once here, not repeated by the page's second form's summary.
                ModelState.AddModelError("Profile.TimeZone", ex.Body);
                return View(await RestoreDisplayFieldsAsync(value));
            }

            await RefreshTimeZoneClaimAsync(value.Profile.TimeZone);
            TempData["ProfileMessage"] = "Profile saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleUIMode(string? returnUrl)
        {
            User self = await api.UserGetSelf();
            UIMode newMode = self.UIMode == UIMode.Simple ? UIMode.Advanced : UIMode.Simple;
            await api.UserProfileSet(new UserProfileUpdate
            {
                FirstName = self.FirstName,
                LastName = self.LastName,
                TimeZone = self.TimeZone,
                UIMode = newMode,
            });
            await RefreshUIModeClaimAsync(newMode);
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> NotificationPreferenceToggle(NotificationEventType eventType, string channel, bool enabled)
        {
            await api.NotificationPreferenceSet(new UserNotificationPreference { EventType = eventType, Channel = channel, Enabled = enabled });
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DevicePin()
        {
            await api.DevicePinGenerate();
            TempData["ProfileMessage"] = "New device PIN generated - it is valid for 24 hours and can be used to register as many devices as you need in that window.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel value)
        {
            User self = await api.UserGetSelf();
            if (ModelState.IsValid)
            {
                try
                {
                    // Identity comes from the caller's own JWT server-side, never from the form.
                    await api.ChangePassword(new UserSetPassword
                    {
                        OldPassword = value.OldPassword,
                        NewPassword = value.NewPassword,
                    });
                    TempData["ProfileMessage"] = "Password changed.";
                    return RedirectToAction(nameof(Index));
                }
                catch (ApiException ex)
                {
                    ModelState.AddModelError(nameof(ChangePasswordViewModel.OldPassword), ex.Body);
                }
            }

            ProfileViewModel model = BuildViewModel(self);
            model.NotificationPreferences = await api.NotificationPreferencesGet();
            return View(nameof(Index), model);
        }

        private async Task<ProfileViewModel> BuildViewModelAsync()
        {
            User self = await api.UserGetSelf();
            ProfileViewModel model = BuildViewModel(self);
            model.NotificationPreferences = await api.NotificationPreferencesGet();
            return model;
        }

        private static ProfileViewModel BuildViewModel(User self) => new()
        {
            Email = self.Email,
            Profile = new UserProfileUpdate
            {
                FirstName = self.FirstName,
                LastName = self.LastName,
                TimeZone = self.TimeZone,
                UIMode = self.UIMode,
            },
            TimeZones = TimeZoneOptions(self.TimeZone),
            DevicePin = self.DevicePin,
            DevicePinExpires = self.DevicePinExpires,
        };

        // PIN fields are display-only (never posted back); refetch them so an error re-render doesn't blank the card.
        private async Task<ProfileViewModel> RestoreDisplayFieldsAsync(ProfileViewModel value)
        {
            User self = await api.UserGetSelf();
            value.Email = self.Email;
            value.DevicePin = self.DevicePin;
            value.DevicePinExpires = self.DevicePinExpires;
            value.TimeZones = TimeZoneOptions(value.Profile.TimeZone);
            value.NotificationPreferences = await api.NotificationPreferencesGet();
            return value;
        }

        private static List<SelectListItem> TimeZoneOptions(string? selected) =>
            TimeZoneHelper.GetTimeZoneOptions()
                .Select(o => new SelectListItem(o.DisplayName, o.Id, string.Equals(o.Id, selected, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        // Re-issues the auth cookie with the new TimeZone claim so it takes effect immediately, not just after the next token refresh.
        private async Task RefreshTimeZoneClaimAsync(string? timeZone)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Principal is null || auth.Properties is null)
            {
                return;
            }

            var claims = auth.Principal.Claims.Where(c => c.Type != UserClaims.TimeZone).ToList();
            if (!string.IsNullOrEmpty(timeZone))
            {
                claims.Add(new Claim(UserClaims.TimeZone, timeZone));
            }

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), auth.Properties);
        }

        // Same "re-issue the cookie right now" reasoning as RefreshTimeZoneClaimAsync above - the nav menu and rule builder read this claim on every page render, not just after the next token refresh.
        private async Task RefreshUIModeClaimAsync(UIMode uiMode)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Principal is null || auth.Properties is null)
            {
                return;
            }

            var claims = auth.Principal.Claims.Where(c => c.Type != UserClaims.UIMode).ToList();
            claims.Add(new Claim(UserClaims.UIMode, uiMode.ToString()));

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), auth.Properties);
        }
    }
}
