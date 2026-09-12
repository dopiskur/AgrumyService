using Agrumy.Shared.Models;

namespace Agrumy.Api.Utils
{
    /// Pure computation of SensorDataArchiveEvaluator's cutoff - rows with DateCreated strictly before the result move to the archive database. Kept separate from the evaluator so the three modes are testable without a database.
    public static class ArchiveCutoffCalculator
    {
        /// Null means "not configured yet" - CustomDate with no date set, or CustomRollingDays with no value set; the caller must treat that as nothing to archive this run, not an error.
        public static DateTimeOffset? ComputeCutoffUtc(ServerConfig config, DateTimeOffset nowUtc) => config.ArchiveCutoffMode switch
        {
            // Two calendar years back - e.g. on 2027-01-01, everything before 2026-01-01 (all of 2025 and earlier) is archived, active keeps 2026+.
            ArchiveCutoffMode.Calendar => new DateTimeOffset(nowUtc.Year - 1, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ArchiveCutoffMode.CustomDate => config.ArchiveCustomCutoffDate is DateOnly d
                ? new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero)
                : null,
            ArchiveCutoffMode.CustomRollingDays => config.ArchiveCustomRollingDays is int days ? nowUtc.AddDays(-days) : null,
            _ => null,
        };
    }
}
