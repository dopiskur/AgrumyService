using System.Security.Claims;
using Agrumy.Shared.Models;

namespace Agrumy.Web.Security
{
    public static class UserClaims
    {
        public const string TimeZone = "TimeZone";
        public const string UIMode = "UIMode";

        /// Cached at sign-in so pages don't round-trip to Agrumy.Api just to convert a UTC timestamp for display.
        public static string? GetTimeZone(this ClaimsPrincipal principal) => principal.FindFirst(TimeZone)?.Value;

        /// Cached at sign-in same as GetTimeZone above - the shared _Layout reads this on every page. Missing/unparsable claim defaults to Simple, the same default a fresh user row has.
        public static Agrumy.Shared.Models.UIMode GetUIMode(this ClaimsPrincipal principal) =>
            Enum.TryParse(principal.FindFirst(UIMode)?.Value, out Agrumy.Shared.Models.UIMode mode) ? mode : Agrumy.Shared.Models.UIMode.Simple;
    }
}
