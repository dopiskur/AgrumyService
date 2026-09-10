using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IUserRepository activation members - forwarded to the standalone EfUserRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task UserSetActivationTokenAsync(int idUser, string tokenHash, DateTime expiresAt) =>
            userRepository.UserSetActivationTokenAsync(idUser, tokenHash, expiresAt);

        public Task<bool> UserIssueActivationTokenAsync(int idUser, string tokenHash, DateTime expiresAt, int cooldownMinutes) =>
            userRepository.UserIssueActivationTokenAsync(idUser, tokenHash, expiresAt, cooldownMinutes);

        public Task<User?> UserActivateAsync(string tokenHash) => userRepository.UserActivateAsync(tokenHash);
    }
}
