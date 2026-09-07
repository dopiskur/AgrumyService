using Agrumy.Shared.Models;
using Agrumy.Api.Utils;

namespace Agrumy.Api.Tests;

public class ArchiveCutoffCalculatorTests
{
    [Fact]
    public void Calendar_OnJan1_ArchivesTwoCalendarYearsBackAndOlder()
    {
        var config = new ServerConfig { ArchiveCutoffMode = ArchiveCutoffMode.Calendar };
        var now = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset? cutoff = ArchiveCutoffCalculator.ComputeCutoffUtc(config, now);

        // 2025 and earlier archived, 2026+ stays active.
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Fact]
    public void Calendar_MidYear_SameCutoffAsJan1()
    {
        var config = new ServerConfig { ArchiveCutoffMode = ArchiveCutoffMode.Calendar };
        var now = new DateTimeOffset(2027, 7, 15, 12, 0, 0, TimeSpan.Zero);

        DateTimeOffset? cutoff = ArchiveCutoffCalculator.ComputeCutoffUtc(config, now);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Fact]
    public void CustomDate_UsesConfiguredDateAtUtcMidnight()
    {
        var config = new ServerConfig { ArchiveCutoffMode = ArchiveCutoffMode.CustomDate, ArchiveCustomCutoffDate = new DateOnly(2025, 6, 1) };

        DateTimeOffset? cutoff = ArchiveCutoffCalculator.ComputeCutoffUtc(config, DateTimeOffset.UtcNow);

        Assert.Equal(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Fact]
    public void CustomDate_NotSet_ReturnsNull()
    {
        var config = new ServerConfig { ArchiveCutoffMode = ArchiveCutoffMode.CustomDate, ArchiveCustomCutoffDate = null };

        Assert.Null(ArchiveCutoffCalculator.ComputeCutoffUtc(config, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CustomRollingDays_SubtractsFromNow()
    {
        var config = new ServerConfig { ArchiveCutoffMode = ArchiveCutoffMode.CustomRollingDays, ArchiveCustomRollingDays = 90 };
        var now = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        DateTimeOffset? cutoff = ArchiveCutoffCalculator.ComputeCutoffUtc(config, now);

        Assert.Equal(now.AddDays(-90), cutoff);
    }

    [Fact]
    public void CustomRollingDays_NotSet_ReturnsNull()
    {
        var config = new ServerConfig { ArchiveCutoffMode = ArchiveCutoffMode.CustomRollingDays, ArchiveCustomRollingDays = null };

        Assert.Null(ArchiveCutoffCalculator.ComputeCutoffUtc(config, DateTimeOffset.UtcNow));
    }
}
