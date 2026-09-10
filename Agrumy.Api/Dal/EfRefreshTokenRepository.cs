using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// IRefreshTokenRepository - a leaf facet, only called into (by EfUserRepository), never calls out.
    internal sealed class EfRefreshTokenRepository(AgrumyDbContext db) : IRefreshTokenRepository
    {
        public async Task<int> RefreshTokenAddAsync(int userID, string tokenHash, DateTime expiresAt)
        {
            var row = new RefreshTokenRow
            {
                UserID = userID,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
            };
            db.RefreshTokens.Add(row);
            await db.SaveChangesAsync();
            return row.IDRefreshToken;
        }

        public async Task<RefreshTokenInfo?> RefreshTokenGetAsync(string tokenHash)
        {
            var row = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
            return row == null
                ? null
                : new RefreshTokenInfo { UserID = row.UserID, ExpiresAt = row.ExpiresAt, RevokedAt = row.RevokedAt };
        }

        public async Task<bool> RefreshTokenRotateAsync(int userId, string oldTokenHash, string newTokenHash, DateTime newExpiresAt)
        {
            // Same atomic WHERE RevokedAt == null guard as RefreshTokenRevokeAsync - only one of two racing redemptions can affect a row.
            int rows = await db.RefreshTokens.Where(t => t.TokenHash == oldTokenHash && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, DateTime.UtcNow)
                    .SetProperty(t => t.ReplacedByTokenHash, newTokenHash));
            if (rows == 0)
            {
                return false;
            }

            db.RefreshTokens.Add(new RefreshTokenRow
            {
                UserID = userId,
                TokenHash = newTokenHash,
                ExpiresAt = newExpiresAt,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return true;
        }

        public async Task RefreshTokenRevokeAsync(string tokenHash)
        {
            await db.RefreshTokens.Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        }

        public async Task RefreshTokenRevokeAllForUserAsync(int userID)
        {
            await db.RefreshTokens.Where(t => t.UserID == userID && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        }
    }
}
