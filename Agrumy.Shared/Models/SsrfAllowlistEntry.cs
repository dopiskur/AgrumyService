using System.ComponentModel.DataAnnotations;

namespace Agrumy.Shared.Models
{
    /// One admin-configured exception to SsrfGuard's default block - Pattern is either an exact hostname or a CIDR range (e.g. "192.168.1.0/24"), auto-detected by SsrfGuard.IsCidrPattern. Firmware and Webhook keep their own separate list (own DB table), never shared, so whitelisting one outbound target never widens the other.
    public class SsrfAllowlistEntry
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Hostname or CIDR range")]
        public string Pattern { get; set; } = "";

        /// Relaxes SsrfGuard's private/reserved-IP block for requests matching this entry - independent of AllowInsecureHttp.
        [Display(Name = "Allow private/LAN address")]
        public bool AllowPrivateNetwork { get; set; }

        /// Relaxes SsrfGuard's https-only requirement for requests matching this entry - independent of AllowPrivateNetwork.
        [Display(Name = "Allow plain http")]
        public bool AllowInsecureHttp { get; set; }
    }
}
