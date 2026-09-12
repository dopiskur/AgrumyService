using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Notifications
{
    /// Shared "organization admins, minus anyone who opted out of this event type" recipient list - used by every alert-generating background worker (OfflineAlertEvaluator, LowBatteryAlertEvaluator, TankRefillAlertEvaluator, RuleNotificationEvaluator) so the per-user opt-out actually applies uniformly instead of each evaluator re-deriving its own admin list.
    public static class NotificationRecipientBuilder
    {
        public static async Task<IReadOnlyList<NotificationRecipient>> BuildForTenantAdminsAsync(IUserRepository userRepo, int tenantId, NotificationEventType eventType)
        {
            IList<User> admins = await userRepo.TenantAdminsGetAsync(tenantId);
            return await FilterAsync(userRepo, admins, eventType);
        }

        /// Public separately from BuildForTenantAdminsAsync so a caller that already has its own user list (none today, but keeps the two concerns - "who are the admins" vs "who opted out" - independently testable) can still apply the same filter.
        public static async Task<IReadOnlyList<NotificationRecipient>> FilterAsync(IUserRepository userRepo, IList<User> users, NotificationEventType eventType)
        {
            var withEmail = users.Where(u => !string.IsNullOrWhiteSpace(u.Email) && u.IDUser is int).ToList();
            if (withEmail.Count == 0)
            {
                return Array.Empty<NotificationRecipient>();
            }

            var disabled = await userRepo.NotificationDisabledChannelsGetAsync(withEmail.Select(u => u.IDUser!.Value), eventType);
            return withEmail
                .Where(u => !(disabled.TryGetValue(u.IDUser!.Value, out var channels) && channels.Contains(NotificationChannels.Email)))
                .Select(u => new NotificationRecipient(Email: u.Email))
                .ToList();
        }
    }
}
