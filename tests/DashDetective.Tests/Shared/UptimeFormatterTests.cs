using DashDetective.Shared;
using System;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="UptimeFormatter.Format"/>: the "Nd Nh Nm" rollovers, leading-zero-unit
/// dropping, and the sub-minute / zero edge.</summary>
public class UptimeFormatterTests {
    [Fact]
    public void Format_DaysPresent_ShowsAllThreeUnits() {
        Assert.Equal("3d 14h 22m", UptimeFormatter.Format(new TimeSpan(3, 14, 22, 0)));
    }

    [Fact]
    public void Format_ZeroDays_DropsDaysUnit() {
        Assert.Equal("5h 2m", UptimeFormatter.Format(new TimeSpan(0, 5, 2, 0)));
    }

    [Fact]
    public void Format_ZeroDaysAndHours_ShowsMinutesOnly() {
        Assert.Equal("12m", UptimeFormatter.Format(new TimeSpan(0, 0, 12, 0)));
    }

    [Fact]
    public void Format_DaysWithZeroHoursAndMinutes_KeepsTrailingUnits() {
        // Only *leading* zero units are dropped; with days present, h and m stay even at zero.
        Assert.Equal("1d 0h 0m", UptimeFormatter.Format(new TimeSpan(1, 0, 0, 0)));
    }

    [Fact]
    public void Format_UnderOneMinute_ShowsZeroMinutes() {
        Assert.Equal("0m", UptimeFormatter.Format(new TimeSpan(0, 0, 0, 45)));
    }

    [Fact]
    public void Format_Zero_ShowsZeroMinutes() {
        Assert.Equal("0m", UptimeFormatter.Format(TimeSpan.Zero));
    }

    [Fact]
    public void Format_TruncatesTotalDays() {
        // 1 day 23 h → (int)TotalDays = 1, remainder shows as hours.
        Assert.Equal("1d 23h 0m", UptimeFormatter.Format(new TimeSpan(1, 23, 0, 0)));
    }
}
