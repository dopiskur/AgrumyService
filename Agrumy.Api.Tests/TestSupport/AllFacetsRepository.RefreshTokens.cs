using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IRefreshTokenRepository members - forwarded to the standalone EfRefreshTokenRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<int> RefreshTokenAddAsync(int userID, string tokenHash, DateTime expiresAt) =>
            refreshTokenRepository.RefreshTokenAddAsync(userID, tokenHash, expiresAt);

        public Task<RefreshTokenInfo?> RefreshTokenGetAsync(string tokenHash) => refreshTokenRepository.RefreshTokenGetAsync(tokenHash);

        public Task<bool> RefreshTokenRotateAsync(int userId, string oldTokenHash, string newTokenHash, DateTime newExpiresAt) =>
            refreshTokenRepository.RefreshTokenRotateAsync(userId, oldTokenHash, newTokenHash, newExpiresAt);

        public Task RefreshTokenRevokeAsync(string tokenHash) => refreshTokenRepository.RefreshTokenRevokeAsync(tokenHash);

        public Task RefreshTokenRevokeAllForUserAsync(int userID) => refreshTokenRepository.RefreshTokenRevokeAllForUserAsync(userID);
    }
}
