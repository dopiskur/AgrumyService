using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IUserRepository role members - forwarded to the standalone EfUserRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<IList<UserRole>> UserRoleGetAsync() => userRepository.UserRoleGetAsync();

        public Task<IReadOnlyList<string>> UserRoleNamesGetAsync(int idUser) => userRepository.UserRoleNamesGetAsync(idUser);

        public Task UserRolesSetAsync(int idUser, IEnumerable<string> roleNames) => userRepository.UserRolesSetAsync(idUser, roleNames);
    }
}
