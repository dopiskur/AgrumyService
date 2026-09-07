using Agrumy.Shared.Models;
using Refit;

namespace Agrumy.Web.Dal.Interface
{
    // Deliberately registered WITHOUT BearerTokenHandler - routing through it here would recurse back into itself.
    public interface IAuthApi
    {
        [Post("/api/User/RefreshToken")]
        Task<UserLoginResult> RefreshToken([Body] RefreshTokenRequest request, CancellationToken ct = default);

        [Post("/api/User/RevokeRefreshToken")]
        Task RevokeRefreshToken([Body] RefreshTokenRequest request);
    }
}
