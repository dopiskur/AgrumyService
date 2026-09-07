using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public class AuditLogViewModel
    {
        public IReadOnlyList<AuditLogEntry> Entries { get; set; } = new List<AuditLogEntry>();
        public string? ActorEmail { get; set; }
        public string? Action { get; set; }
        public string? TargetType { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
    }
}
