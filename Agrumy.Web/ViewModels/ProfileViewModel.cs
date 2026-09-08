using System.ComponentModel.DataAnnotations;
using Agrumy.Shared.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Agrumy.Web.ViewModels
{
    // Deliberately NOT UserView/UserUpdate, which carry admin-only fields (Enabled/TenantID) this page must never post.
    public class ProfileViewModel
    {
        public string? Email { get; set; }
        public UserProfileUpdate Profile { get; set; } = new();
        public IEnumerable<SelectListItem> TimeZones { get; set; } = [];

        // Display-only; a new PIN is issued via the page's "Generate new PIN" post, never typed in by hand.
        public string? DevicePin { get; set; }
        public DateTimeOffset? DevicePinExpires { get; set; }

        // Full (EventType x Channel) matrix, defaults already resolved server-side - each row renders as one checkbox, toggling posts NotificationPreferenceToggle for just that row.
        public IList<UserNotificationPreference> NotificationPreferences { get; set; } = [];
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Old password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Old Password")]
        public string? OldPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Password confirmation is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; }
    }
}
