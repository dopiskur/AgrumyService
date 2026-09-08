using Agrumy.Rules;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;

namespace Agrumy.Api.Tests;

/// SolarCalculator's NOAA formulas (checked against well-known seasonal invariants, not hardcoded minute-precision tables) and AstronomicalRuleResolver's compile-to-Schedule step (#228).
public class AstronomicalScheduleTests
{
    [Fact]
    public void Compute_Equator_Equinox_DayLength_IsAbout12Hours()
    {
        var (sunrise, sunset) = SolarCalculator.Compute(new DateOnly(2026, 3, 20), latitude: 0, longitude: 0, utcOffsetSeconds: 0);
        Assert.NotNull(sunrise);
        Assert.NotNull(sunset);
        int dayLengthSeconds = sunset!.Value - sunrise!.Value;
        Assert.InRange(dayLengthSeconds, 11 * 3600 + 45 * 60, 12 * 3600 + 15 * 60);
        // Solar noon near local 12:00 at longitude 0.
        Assert.InRange((sunrise.Value + sunset.Value) / 2, 11 * 3600, 13 * 3600);
    }

    [Fact]
    public void Compute_NorthernSummerSolstice_HighLatitude_DayIsMuchLongerThan12Hours()
    {
        var (sunrise, sunset) = SolarCalculator.Compute(new DateOnly(2026, 6, 21), latitude: 60, longitude: 0, utcOffsetSeconds: 0);
        Assert.NotNull(sunrise);
        Assert.NotNull(sunset);
        Assert.True(sunset!.Value - sunrise!.Value > 17 * 3600);
    }

    [Fact]
    public void Compute_SouthernSummerSolstice_MidLatitude_DayIsLongerThan12Hours()
    {
        // December is southern-hemisphere summer - a mid-southern latitude should see a long day, mirroring the northern June case above.
        var (sunrise, sunset) = SolarCalculator.Compute(new DateOnly(2026, 12, 21), latitude: -45, longitude: 0, utcOffsetSeconds: 0);
        Assert.NotNull(sunrise);
        Assert.NotNull(sunset);
        Assert.True(sunset!.Value - sunrise!.Value > 14 * 3600);
    }

    [Fact]
    public void Compute_PolarNight_ReturnsNull()
    {
        var (sunrise, sunset) = SolarCalculator.Compute(new DateOnly(2026, 12, 21), latitude: 80, longitude: 0, utcOffsetSeconds: 0);
        Assert.Null(sunrise);
        Assert.Null(sunset);
    }

    [Fact]
    public void Resolve_NoLocationConfigured_DropsRule()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { AstroRule(daysOfWeek: 127, sunriseOffset: 0, sunsetOffset: 0) };

        IList<DeviceFarmUnitZoneRule> resolved = AstronomicalRuleResolver.Resolve(rules, null, null, new DateOnly(2026, 6, 21), utcOffsetSeconds: 0);

        Assert.Empty(resolved);
    }

    [Fact]
    public void Resolve_WithLocation_CompilesToScheduleWindowSpanningSunriseToSunset()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { AstroRule(daysOfWeek: 127, sunriseOffset: -30, sunsetOffset: 60) };

        IList<DeviceFarmUnitZoneRule> resolved = AstronomicalRuleResolver.Resolve(rules, 45.8, 16.0, new DateOnly(2026, 6, 21), utcOffsetSeconds: 7200);

        DeviceFarmUnitZoneRule rule = Assert.Single(resolved);
        ConditionNode node = rule.Root!;
        Assert.Equal(NodeType.Schedule, node.Type);
        Assert.Equal(127, node.DaysOfWeek);

        var (sunrise, sunset) = SolarCalculator.Compute(new DateOnly(2026, 6, 21), 45.8, 16.0, 7200);
        Assert.Equal(Math.Clamp(sunrise!.Value - 30 * 60, 0, 86399), node.Start);
        Assert.Equal(Math.Clamp(sunset!.Value + 60 * 60, 0, 86400) - node.Start, node.Duration);
    }

    [Fact]
    public void Resolve_OffsetsCollapseWindowToZero_DropsRule()
    {
        // Sunrise offset pushed past the sunset offset on the same day leaves nothing "on".
        var rules = new List<DeviceFarmUnitZoneRule> { AstroRule(daysOfWeek: 127, sunriseOffset: 700, sunsetOffset: -700) };

        IList<DeviceFarmUnitZoneRule> resolved = AstronomicalRuleResolver.Resolve(rules, 45.8, 16.0, new DateOnly(2026, 6, 21), utcOffsetSeconds: 7200);

        Assert.Empty(resolved);
    }

    [Fact]
    public void Resolve_NotificationActionRule_CompilesJustLikeRelay()
    {
        // Roadmap #398(2) - the resolver was always ActionType-agnostic, DeviceFarmUnitApiController's validation was the only thing blocking Astronomical on a Notification rule.
        var rule = AstroRule(daysOfWeek: 127, sunriseOffset: 0, sunsetOffset: 0);
        rule.ActionType = ActionType.Notification;
        rule.RelayFunction = null;
        rule.NotificationSubject = "CO2 low";

        IList<DeviceFarmUnitZoneRule> resolved = AstronomicalRuleResolver.Resolve([rule], 45.8, 16.0, new DateOnly(2026, 6, 21), utcOffsetSeconds: 7200);

        DeviceFarmUnitZoneRule resolvedRule = Assert.Single(resolved);
        Assert.Equal(ActionType.Notification, resolvedRule.ActionType);
        Assert.Equal(NodeType.Schedule, resolvedRule.Root!.Type);
    }

    [Fact]
    public void Resolve_NonAstronomicalRule_PassesThroughUnchanged()
    {
        var scheduleRule = new DeviceFarmUnitZoneRule
        {
            DeviceFarmUnitZoneID = 1,
            RelayFunction = RelayFunction.Light,
            Root = new ConditionNode { Type = NodeType.Schedule, DaysOfWeek = 127, Start = 0, Duration = 3600 },
        };

        IList<DeviceFarmUnitZoneRule> resolved = AstronomicalRuleResolver.Resolve([scheduleRule], null, null, new DateOnly(2026, 6, 21), 0);

        Assert.Same(scheduleRule, Assert.Single(resolved));
    }

    private static DeviceFarmUnitZoneRule AstroRule(int daysOfWeek, int sunriseOffset, int sunsetOffset) => new()
    {
        DeviceFarmUnitZoneID = 1,
        RelayFunction = RelayFunction.Light,
        Root = new ConditionNode { Type = NodeType.Astronomical, DaysOfWeek = daysOfWeek, SunriseOffsetMinutes = sunriseOffset, SunsetOffsetMinutes = sunsetOffset },
    };
}
