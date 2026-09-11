using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IUserRepository, extracted out of the EfRepository god class - accounts, secrets, composable roles, email activation, and bootstrap admin. RegisterUserAsync needs ITenantRepository (silent tenant-create on registration) and IDeviceFarmUnitRepository (same tenant-create branch also seeds the tenant's first farm), RevokeUserTokensAsync needs IRefreshTokenRepository (and ICache, to invalidate Agrumy.Api.Security.TokenRevocationValidator's own cached state) - all already-extracted facets, no circular dependency (neither depends back on IUserRepository).
    internal sealed class EfUserRepository(AgrumyDbContext db, ITenantRepository tenantRepository, IDeviceFarmUnitRepository deviceFarmUnitRepository, IRefreshTokenRepository refreshTokenRepository, ICache cache) : IUserRepository
    {
        /// quotaCheckAsync null (RegisterUserAsync's own internal call) means "already checked by the caller, don't check again" - not "unlimited".
        public Task UserAddAsync(User user, UserSecret userSecret, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                db.Users.Add(new UserRow
                {
                    TenantID = user.TenantID ?? 0,
                    Email = user.Email ?? "",
                    Username = user.Username,
                    DevicePin = user.DevicePin,
                    DevicePinExpires = user.DevicePinExpires,
                    PwdHash = userSecret.PwdHash ?? "",
                    PwdSalt = userSecret.PwdSalt ?? "",
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Phone = user.Phone,
                    PhoneEnabled = user.PhoneEnabled ?? true,
                    Enabled = user.Enabled,
                    EmailVerified = user.EmailVerified ?? false,
                    MustChangePassword = user.MustChangePassword,
                });
                await db.SaveChangesAsync();
                return true;
            });

        // Same retry count/reasoning as Agrumy.Api.Quota.QuotaGuard.RunAsync - this method can't call that helper directly (the quota check needs user.TenantID, only known once the tenant-create branch above has run inside the same transaction), so it inlines the identical Serializable-transaction-plus-Contention-retry shape instead.
        private const int MaxContentionRetries = 3;

        public async Task<int> RegisterUserAsync(User user, UserSecret userSecret, int? existingTenantId, string? newTenantName,
            string activationTokenHash, DateTime activationTokenExpiresAtUtc, IEnumerable<string> startingRoles,
            Func<int?, Task<string?>>? quotaCheckAsync = null)
        {
            for (int attempt = 1; ; attempt++)
            {
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    bool isNewTenant = existingTenantId is null;
                    user.TenantID = existingTenantId ?? await tenantRepository.TenantAddAsync(newTenantName!);
                    if (isNewTenant)
                    {
                        await deviceFarmUnitRepository.EnsureFirstFarmAsync(user.TenantID.Value);
                    }

                    // Checked inside this same transaction, not by the caller beforehand - a plain pre-check would let two concurrent registrations into the same near-full tenant both read "one seat free" and both take it.
                    if (quotaCheckAsync != null && await quotaCheckAsync(user.TenantID) is string limitError)
                    {
                        await transaction.RollbackAsync();
                        throw new QuotaLimitExceededException(limitError);
                    }

                    await UserAddAsync(user, userSecret);

                    // UserAddAsync doesn't return the new IDUser - re-fetch by the just-inserted unique email.
                    User added = await UserGetAsync(null, user.Email, null)
                        ?? throw new InvalidOperationException("UserAddAsync did not persist the expected row.");
                    await UserSetActivationTokenAsync(added.IDUser!.Value, activationTokenHash, activationTokenExpiresAtUtc);
                    await UserRolesSetAsync(added.IDUser.Value, startingRoles);

                    await transaction.CommitAsync();
                    return added.IDUser.Value;
                }
                catch (Exception ex) when (DbExceptionClassifier.Classify(ex) == DbFailureKind.Contention && attempt < MaxContentionRetries)
                {
                    await transaction.RollbackAsync();
                }
            }
        }

        public async Task UserUpdateAsync(User user)
        {
            var row = await db.Users.FirstOrDefaultAsync(u => u.IDUser == user.IDUser);
            if (row == null)
            {
                return;
            }

            row.TenantID = user.TenantID ?? 0;
            string newEmail = user.Email ?? "";
            // Changing the address invalidates verification of the OLD one - without this an unconfirmed new address inherits the old address's verified status.
            if (!string.Equals(row.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                row.EmailVerified = false;
            }
            row.Email = newEmail;
            // DevicePin deliberately NOT written here - the PIN lifecycle lives exclusively in UserSetDevicePinAsync, so an admin edit can never resurrect an expired PIN or hand-craft a weak one.
            row.Username = user.Username;
            row.FirstName = user.FirstName;
            row.LastName = user.LastName;
            row.Phone = user.Phone;
            row.PhoneEnabled = user.PhoneEnabled;
            row.Enabled = user.Enabled;
            row.TimeZone = user.TimeZone;
            await db.SaveChangesAsync();
        }

        /// Self-service profile write - deliberately touches only these profile columns, so the endpoint can't alter Enabled/TenantID even if the controller mis-binds.
        public async Task<bool> UserProfileSetAsync(string email, string? firstName, string? lastName, string? timeZone, UIMode uiMode)
        {
            var row = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (row == null)
            {
                return false;
            }

            row.FirstName = firstName;
            row.LastName = lastName;
            row.TimeZone = timeZone;
            row.UIMode = (int)uiMode;
            await db.SaveChangesAsync();
            return true;
        }

        /// The only writer of DevicePin/DevicePinExpires - a successful device registration does NOT call this, since the PIN is multi-use within its 24h window, not consumed on first use.
        public async Task<bool> UserSetDevicePinAsync(int idUser, string? devicePin, DateTime? expiresAtUtc)
        {
            var row = await db.Users.FirstOrDefaultAsync(u => u.IDUser == idUser);
            if (row == null)
            {
                return false;
            }

            row.DevicePin = devicePin;
            row.DevicePinExpires = expiresAtUtc;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UserDeleteAsync(int? idUser)
        {
            // Protects the default admin/user (id 1). Callers already enforce this, but keep it here too.
            if (idUser is null or <= 1)
            {
                return false;
            }

            // userRefreshToken's FK is NoAction (RevokeAllForUserAsync only sets RevokedAt, never deletes rows) - without this, deleting a user who has ever logged in throws a FK violation instead of succeeding. userUserRole's FK is Cascade, so it needs no explicit cleanup here.
            await db.RefreshTokens.Where(t => t.UserID == idUser).ExecuteDeleteAsync();

            int rows = await db.Users.Where(u => u.IDUser == idUser).ExecuteDeleteAsync();
            return rows > 0;
        }

        public async Task<User?> UserGetAsync(int? idUser, string? email, string? username)
        {
            IQueryable<UserRow> q = db.Users.AsNoTracking();

            if (idUser != null)
            {
                q = q.Where(u => u.IDUser == idUser);
            }
            else if (idUser == null && email != null && username == null)
            {
                q = q.Where(u => u.Email == email);
            }
            else if (idUser == null && email == null && username != null)
            {
                q = q.Where(u => u.Username == username);
            }
            else
            {
                throw new ArgumentException("Provide an id, email, or username to look a user up by.");
            }

            var hit = await q.FirstOrDefaultAsync();
            return hit == null ? null : ToDto(hit);
        }

        public async Task<IList<User>> UsersGetAsync(int? tenantID)
        {
            var rows = await db.Users.AsNoTracking().Where(u => u.TenantID == tenantID).ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        // Same query as UsersGetAsync minus the tenant filter - callers (UserApiController) only reach this after confirming the caller is a TenantID==0 admin.
        public async Task<IList<User>> UsersGetAllAsync()
        {
            var rows = await db.Users.AsNoTracking().ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<UserSecret?> UserSecretGetAsync(int? idUser, string? email, string? username)
        {
            IQueryable<UserRow> q = db.Users.AsNoTracking();

            if (idUser != null)
            {
                q = q.Where(u => u.IDUser == idUser);
            }
            else if (idUser == null && email != null && username == null)
            {
                q = q.Where(u => u.Email == email);
            }
            else if (idUser == null && email == null && username != null)
            {
                q = q.Where(u => u.Username == username);
            }
            else
            {
                throw new ArgumentException("Provide an id, email, or username to look a secret up by.");
            }

            return await q.Select(u => new UserSecret { PwdHash = u.PwdHash, PwdSalt = u.PwdSalt })
                          .FirstOrDefaultAsync();
        }

        public async Task<bool> UserSetPasswordAsync(string? email, UserSecret userSecret)
        {
            var row = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (row == null)
            {
                return false;
            }

            row.PwdHash = userSecret.PwdHash ?? "";
            row.PwdSalt = userSecret.PwdSalt ?? "";
            // Any successful password change satisfies "you changed your password" - clearing the flag here avoids duplicating the write in every caller.
            row.MustChangePassword = false;
            await db.SaveChangesAsync();
            await RevokeUserTokensAsync(row.IDUser);
            return true;
        }

        public async Task RevokeUserTokensAsync(int idUser)
        {
            await db.Users.Where(u => u.IDUser == idUser)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.TokensValidAfterUtc, DateTime.UtcNow));
            await refreshTokenRepository.RefreshTokenRevokeAllForUserAsync(idUser);

            // Without this, a caller who already had an authenticated request in the last 30s (e.g. the MustChangePassword page itself) keeps TokenRevocationValidator's stale, pre-revoke cached state until that cache entry naturally expires - a revoked token would still pass for up to 30s more.
            string? email = await db.Users.AsNoTracking().Where(u => u.IDUser == idUser).Select(u => u.Email).FirstOrDefaultAsync();
            if (email != null)
            {
                await cache.RemoveAsync(CacheKeys.TokenRevocation(email));
            }
        }

        // Never empty for a real tenant since its creator becomes an admin at registration - TenantID 0 has no owning admin, so Global admin is the equivalent role there.
        public async Task<IList<User>> TenantAdminsGetAsync(int tenantId)
        {
            string adminRoleName = tenantId == 0 ? RoleNames.GlobalAdmin : RoleNames.TenantAdmin;
            var rows = await (from u in db.Users.AsNoTracking()
                              join uur in db.UserUserRoles.AsNoTracking() on u.IDUser equals uur.UserID
                              join r in db.UserRoles.AsNoTracking() on uur.UserRoleID equals r.IDUserRole
                              where u.TenantID == tenantId && r.RoleName == adminRoleName
                              select u).Distinct().ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<bool> BootstrapAdminPendingAsync()
        {
            return await db.Users.AsNoTracking().AnyAsync(u => u.PwdHash == null);
        }

        /// One bulk query for a whole recipient list (e.g. every tenant admin for one alert) rather than one round trip per user - only explicit opt-outs exist as rows, so a user/channel absent from the result stays enabled.
        public async Task<IReadOnlyDictionary<int, HashSet<string>>> NotificationDisabledChannelsGetAsync(IEnumerable<int> userIds, NotificationEventType eventType)
        {
            var ids = userIds.ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, HashSet<string>>();
            }
            var rows = await db.UserNotificationPreferences.AsNoTracking()
                .Where(p => ids.Contains(p.UserID) && p.EventType == (int)eventType && !p.Enabled)
                .ToListAsync();
            return rows.GroupBy(p => p.UserID).ToDictionary(g => g.Key, g => g.Select(p => p.Channel).ToHashSet());
        }

        /// Every explicit override for one user, across every event type/channel - Web's notification-preferences page overlays these onto the full (EventType x Channel) matrix, defaulting anything absent to enabled.
        public async Task<IList<UserNotificationPreference>> NotificationPreferencesGetForUserAsync(int userId)
        {
            var rows = await db.UserNotificationPreferences.AsNoTracking().Where(p => p.UserID == userId).ToListAsync();
            return rows.Select(r => new UserNotificationPreference { UserID = r.UserID, EventType = (NotificationEventType)r.EventType, Channel = r.Channel, Enabled = r.Enabled }).ToList();
        }

        /// enabled=true (the default) deletes any existing override instead of storing it, keeping the table an opt-out-only record - see UserNotificationPreferenceRow's own remarks.
        public async Task NotificationPreferenceSetAsync(int userId, NotificationEventType eventType, string channel, bool enabled)
        {
            var existing = await db.UserNotificationPreferences.FirstOrDefaultAsync(p => p.UserID == userId && p.EventType == (int)eventType && p.Channel == channel);
            if (enabled)
            {
                if (existing != null)
                {
                    db.UserNotificationPreferences.Remove(existing);
                    await db.SaveChangesAsync();
                }
                return;
            }
            if (existing != null)
            {
                existing.Enabled = false;
            }
            else
            {
                db.UserNotificationPreferences.Add(new UserNotificationPreferenceRow { UserID = userId, EventType = (int)eventType, Channel = channel, Enabled = false });
            }
            await db.SaveChangesAsync();
        }

        /// Fetch-then-verify-then-write (verification needs C#), but still race-safe since the final write stays gated by WHERE PwdHash IS NULL.
        public async Task<bool> BootstrapAdminSetPasswordAsync(UserSecret secret, string setupSecret)
        {
            UserRow? pending = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PwdHash == null);
            if (pending == null || !AuthenticationProvider.VerifyHash(pending.BootstrapSecretHash, pending.BootstrapSecretSalt, setupSecret))
            {
                return false;
            }

            // WHERE PwdHash IS NULL (not a Login/email match) is what makes the door close permanently once used - clearing BootstrapSecret* isn't load-bearing for replay but leaves no live secret hash lying around.
            int rows = await db.Users.Where(u => u.PwdHash == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.PwdHash, secret.PwdHash)
                    .SetProperty(u => u.PwdSalt, secret.PwdSalt)
                    .SetProperty(u => u.BootstrapSecretHash, (string?)null)
                    .SetProperty(u => u.BootstrapSecretSalt, (string?)null));
            return rows > 0;
        }

        public async Task<bool> BootstrapAdminDiscardPendingAsync()
        {
            int rows = await db.Users.Where(u => u.PwdHash == null).ExecuteDeleteAsync();
            return rows > 0;
        }

        public async Task<IList<UserRole>> UserRoleGetAsync()
        {
            return await db.UserRoles.AsNoTracking()
                .Select(r => new UserRole { IDUserRole = r.IDUserRole, RoleName = r.RoleName })
                .ToListAsync();
        }

        public async Task<IReadOnlyList<string>> UserRoleNamesGetAsync(int idUser)
        {
            return await (from ur in db.UserUserRoles.AsNoTracking()
                          join r in db.UserRoles.AsNoTracking() on ur.UserRoleID equals r.IDUserRole
                          where ur.UserID == idUser && r.RoleName != null
                          select r.RoleName!).ToListAsync();
        }

        public async Task UserRolesSetAsync(int idUser, IEnumerable<string> roleNames)
        {
            var wanted = roleNames.ToHashSet();

            var roleIds = await db.UserRoles.AsNoTracking()
                .Where(r => r.RoleName != null && wanted.Contains(r.RoleName))
                .Select(r => r.IDUserRole)
                .ToListAsync();

            var existing = await db.UserUserRoles.Where(x => x.UserID == idUser).ToListAsync();
            db.UserUserRoles.RemoveRange(existing.Where(x => !roleIds.Contains(x.UserRoleID)));
            db.UserUserRoles.AddRange(roleIds
                .Where(id => existing.All(x => x.UserRoleID != id))
                .Select(id => new UserUserRoleRow { UserID = idUser, UserRoleID = id }));

            await db.SaveChangesAsync();
        }

        public async Task UserSetActivationTokenAsync(int idUser, string tokenHash, DateTime expiresAt)
        {
            var row = await db.Users.FirstOrDefaultAsync(u => u.IDUser == idUser);
            if (row is null) { return; }

            row.ActivationTokenHash = tokenHash;
            row.ActivationTokenExpiresAt = expiresAt;
            row.ActivationLastSentAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        public async Task<bool> UserIssueActivationTokenAsync(int idUser, string tokenHash, DateTime expiresAt, int cooldownMinutes)
        {
            var row = await db.Users.FirstOrDefaultAsync(u => u.IDUser == idUser);
            if (row is null || row.EmailVerified)
            {
                return false;
            }
            if (row.ActivationLastSentAt is DateTimeOffset lastSent && lastSent > DateTime.UtcNow.AddMinutes(-cooldownMinutes))
            {
                return false; // still in cooldown
            }

            row.ActivationTokenHash = tokenHash;
            row.ActivationTokenExpiresAt = expiresAt;
            row.ActivationLastSentAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<User?> UserActivateAsync(string tokenHash)
        {
            var row = await db.Users.FirstOrDefaultAsync(u => u.ActivationTokenHash == tokenHash);
            if (row is null || row.ActivationTokenExpiresAt is null || row.ActivationTokenExpiresAt < DateTime.UtcNow)
            {
                return null;
            }

            row.EmailVerified = true;
            row.ActivationTokenHash = null;
            row.ActivationTokenExpiresAt = null;
            await db.SaveChangesAsync();

            return ToDto(row);
        }

        private static User ToDto(UserRow u) => new()
        {
            IDUser = u.IDUser,
            TenantID = u.TenantID,
            Email = u.Email,
            Username = u.Username,
            DevicePin = u.DevicePin,
            DevicePinExpires = u.DevicePinExpires,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Phone = u.Phone,
            PhoneEnabled = u.PhoneEnabled,
            Enabled = u.Enabled,
            DateCreated = u.DateCreated,
            DateModified = u.DateModified,
            EmailVerified = u.EmailVerified,
            TimeZone = u.TimeZone,
            UIMode = (UIMode)u.UIMode,
            MustChangePassword = u.MustChangePassword,
            TokensValidAfterUtc = u.TokensValidAfterUtc,
        };
    }
}
