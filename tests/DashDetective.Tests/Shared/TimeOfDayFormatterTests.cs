using DashDetective.Shared;
using System;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="TimeOfDayFormatter"/>: both format arms with and without seconds, the 12/24
/// midnight and noon edges where a naive "h" format reads as 0, the invariant AM/PM designators, and the
/// delay to the next displayed change that lets a minute-only clock flip on the minute.</summary>
public class TimeOfDayFormatterTests {
    [Fact]
    public void Format_TwentyFourHour_PadsToTwoDigits() {
        Assert.Equal("04:05:09",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 4, 5, 9), ClockFormat.TwentyFourHour));
    }

    [Fact]
    public void Format_TwentyFourHour_KeepsAfternoonHours() {
        Assert.Equal("16:05:09",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 16, 5, 9), ClockFormat.TwentyFourHour));
    }

    [Fact]
    public void Format_TwelveHour_DropsTheLeadingZero() {
        Assert.Equal("4:05:09 AM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 4, 5, 9), ClockFormat.TwelveHour));
    }

    [Fact]
    public void Format_TwelveHour_ConvertsAfternoonAndMarksPm() {
        Assert.Equal("4:05:09 PM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 16, 5, 9), ClockFormat.TwelveHour));
    }

    /// <summary>Midnight is 12 AM, not 0 AM — the edge a hand-rolled "hour % 12" gets wrong.</summary>
    [Fact]
    public void Format_TwelveHour_Midnight_IsTwelveAm() {
        Assert.Equal("12:00:00 AM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 0, 0, 0), ClockFormat.TwelveHour));
    }

    /// <summary>Noon is 12 PM, the other end of the same edge.</summary>
    [Fact]
    public void Format_TwelveHour_Noon_IsTwelvePm() {
        Assert.Equal("12:00:00 PM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 12, 0, 0), ClockFormat.TwelveHour));
    }

    [Fact]
    public void Format_TwelveHour_LastMinuteOfTheDay_StaysPm() {
        Assert.Equal("11:59:59 PM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 23, 59, 59), ClockFormat.TwelveHour));
    }

    [Fact]
    public void Format_TwentyFourHour_WithoutSeconds_DropsThem() {
        Assert.Equal("16:05",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 16, 5, 9), ClockFormat.TwentyFourHour, false));
    }

    [Fact]
    public void Format_TwelveHour_WithoutSeconds_DropsThem() {
        Assert.Equal("3:41 PM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 15, 41, 7), ClockFormat.TwelveHour, false));
    }

    [Fact]
    public void Format_TwelveHour_WithSeconds_KeepsThem() {
        Assert.Equal("3:41:07 PM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 15, 41, 7), ClockFormat.TwelveHour, true));
    }

    [Fact]
    public void Format_WithoutSeconds_MidnightAndNoon_ReadTwelve() {
        Assert.Equal("12:00 AM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 0, 0, 0), ClockFormat.TwelveHour, false));
        Assert.Equal("12:00 PM",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 12, 0, 0), ClockFormat.TwelveHour, false));
        Assert.Equal("00:00",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 0, 0, 0), ClockFormat.TwentyFourHour, false));
    }

    /// <summary>Seconds are the default, so the Toolkit log (which never passes the flag) keeps them.</summary>
    [Fact]
    public void Format_OmittedFlag_ShowsSeconds() {
        Assert.Equal("16:05:09",
            TimeOfDayFormatter.Format(new DateTime(2026, 8, 29, 16, 5, 9), ClockFormat.TwentyFourHour));
    }

    [Fact]
    public void DelayToNextTick_WithSeconds_IsTheRestOfTheSecond() {
        var now = new DateTime(2026, 8, 29, 16, 5, 9, 250);

        Assert.Equal(TimeSpan.FromMilliseconds(750), TimeOfDayFormatter.DelayToNextTick(now, true));
    }

    /// <summary>The point of the whole setting: at 16:05:09.250 the next change is the 16:06:00 flip.</summary>
    [Fact]
    public void DelayToNextTick_WithoutSeconds_IsTheRestOfTheMinute() {
        var now = new DateTime(2026, 8, 29, 16, 5, 9, 250);

        Assert.Equal(TimeSpan.FromSeconds(50.75), TimeOfDayFormatter.DelayToNextTick(now, false));
    }

    /// <summary>Exactly on a boundary waits a full period rather than firing again at once.</summary>
    [Fact]
    public void DelayToNextTick_OnABoundary_IsAFullPeriod() {
        var now = new DateTime(2026, 8, 29, 16, 5, 0);

        Assert.Equal(TimeSpan.FromSeconds(1), TimeOfDayFormatter.DelayToNextTick(now, true));
        Assert.Equal(TimeSpan.FromMinutes(1), TimeOfDayFormatter.DelayToNextTick(now, false));
    }

    [Fact]
    public void DelayToNextTick_LastSecondOfTheDay_LandsOnMidnight() {
        var now = new DateTime(2026, 8, 29, 23, 59, 59, 900);

        Assert.Equal(TimeSpan.FromMilliseconds(100), TimeOfDayFormatter.DelayToNextTick(now, true));
        Assert.Equal(TimeSpan.FromMilliseconds(100), TimeOfDayFormatter.DelayToNextTick(now, false));
    }

    /// <summary>The default is the format every clock in the app used before the setting existed.</summary>
    [Fact]
    public void Default_IsTwentyFourHour() {
        Assert.Equal(ClockFormat.TwentyFourHour, default(ClockFormat));
    }
}
