using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IUserRepository bootstrap-admin members - forwarded to the standalone EfUserRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<bool> BootstrapAdminPendingAsync() => userRepository.BootstrapAdminPendingAsync();

        public Task<bool> BootstrapAdminSetPasswordAsync(UserSecret secret, string setupSecret) => userRepository.BootstrapAdminSetPasswordAsync(secret, setupSecret);

        public Task<bool> BootstrapAdminDiscardPendingAsync() => userRepository.BootstrapAdminDiscardPendingAsync();
    }
}
