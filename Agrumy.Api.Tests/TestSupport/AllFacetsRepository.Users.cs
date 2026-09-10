using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IUserRepository core members - forwarded to the standalone EfUserRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task UserAddAsync(User user, UserSecret userSecret, Func<Task<string?>>? quotaCheckAsync = null) =>
            userRepository.UserAddAsync(user, userSecret, quotaCheckAsync);

        public Task<int> RegisterUserAsync(User user, UserSecret userSecret, int? existingTenantId, string? newTenantName,
            string activationTokenHash, DateTime activationTokenExpiresAtUtc, IEnumerable<string> startingRoles,
            Func<int?, Task<string?>>? quotaCheckAsync = null) =>
            userRepository.RegisterUserAsync(user, userSecret, existingTenantId, newTenantName, activationTokenHash, activationTokenExpiresAtUtc, startingRoles, quotaCheckAsync);

        public Task UserUpdateAsync(User user) => userRepository.UserUpdateAsync(user);

        public Task<bool> UserProfileSetAsync(string email, string? firstName, string? lastName, string? timeZone, UIMode uiMode) =>
            userRepository.UserProfileSetAsync(email, firstName, lastName, timeZone, uiMode);

        public Task<bool> UserSetDevicePinAsync(int idUser, string? devicePin, DateTime? expiresAtUtc) =>
            userRepository.UserSetDevicePinAsync(idUser, devicePin, expiresAtUtc);

        public Task<bool> UserDeleteAsync(int? idUser) => userRepository.UserDeleteAsync(idUser);

        public Task<User?> UserGetAsync(int? idUser, string? email, string? username) => userRepository.UserGetAsync(idUser, email, username);

        public Task<IList<User>> UsersGetAsync(int? tenantID) => userRepository.UsersGetAsync(tenantID);

        public Task<IList<User>> UsersGetAllAsync() => userRepository.UsersGetAllAsync();

        public Task<UserSecret?> UserSecretGetAsync(int? idUser, string? email, string? username) => userRepository.UserSecretGetAsync(idUser, email, username);

        public Task<bool> UserSetPasswordAsync(string? email, UserSecret userSecret) => userRepository.UserSetPasswordAsync(email, userSecret);

        public Task RevokeUserTokensAsync(int idUser) => userRepository.RevokeUserTokensAsync(idUser);

        public Task<IList<User>> TenantAdminsGetAsync(int tenantId) => userRepository.TenantAdminsGetAsync(tenantId);

        public Task<IReadOnlyDictionary<int, HashSet<string>>> NotificationDisabledChannelsGetAsync(IEnumerable<int> userIds, NotificationEventType eventType) =>
            userRepository.NotificationDisabledChannelsGetAsync(userIds, eventType);

        public Task<IList<UserNotificationPreference>> NotificationPreferencesGetForUserAsync(int userId) => userRepository.NotificationPreferencesGetForUserAsync(userId);

        public Task NotificationPreferenceSetAsync(int userId, NotificationEventType eventType, string channel, bool enabled) =>
            userRepository.NotificationPreferenceSetAsync(userId, eventType, channel, enabled);
    }
}
