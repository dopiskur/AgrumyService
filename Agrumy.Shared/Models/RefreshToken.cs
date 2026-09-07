using System.ComponentModel.DataAnnotations;

namespace api.Models
{
    /// DAL-facing view of a stored refresh token — never the plaintext, only its hash lives in the DB.
    public class RefreshTokenInfo
    {
        public int UserID { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
    }

    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "Refresh token is required")]
        public string? RefreshToken { get; set; }
    }
}
