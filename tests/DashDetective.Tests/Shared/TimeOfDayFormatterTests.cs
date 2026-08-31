using DashDetective.Shared;
using System;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="TimeOfDayFormatter.Format"/>: both arms, the 12/24 midnight and noon edges
/// where a naive "h" format reads as 0, and the invariant AM/PM designators.</summary>
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

    /// <summary>The default is the format every clock in the app used before the setting existed.</summary>
    [Fact]
    public void Default_IsTwentyFourHour() {
        Assert.Equal(ClockFormat.TwentyFourHour, default(ClockFormat));
    }
}
