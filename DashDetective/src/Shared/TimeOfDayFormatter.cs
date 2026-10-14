using System;
using System.Globalization;

namespace DashDetective.Shared;

/// <summary>
/// Shared formatting for on-screen wall-clock times, so the toolbar clock and the Toolkit log read the
/// same and follow one preference. Sits beside <see cref="UptimeFormatter"/>, which covers durations.
///
/// Invariant culture on both arms: the 12-hour arm must say "AM"/"PM" rather than whatever the host
/// locale designates, and the 24-hour arm must not pick up a locale's own separator.
/// </summary>
public static class TimeOfDayFormatter {
    /// <summary>Formats the time of day as "16:05:09" or "4:05:09 PM", or "16:05" / "4:05 PM" without
    /// seconds. Seconds default on because the Toolkit log timestamps commands with them.</summary>
    public static string Format(DateTime time, ClockFormat format, bool showSeconds = true) {
        var pattern = (format == ClockFormat.TwelveHour, showSeconds) switch {
            (true, true) => "h:mm:ss tt",
            (true, false) => "h:mm tt",
            (false, true) => "HH:mm:ss",
            (false, false) => "HH:mm",
        };
        return time.ToString(pattern, CultureInfo.InvariantCulture);
    }

    /// <summary>How long until the displayed time next changes: the next second boundary, or the next
    /// minute boundary without seconds. Lets a minute-only clock flip on the minute, not up to a
    /// minute late.</summary>
    public static TimeSpan DelayToNextTick(DateTime now, bool showSeconds) {
        var unit = showSeconds ? TimeSpan.TicksPerSecond : TimeSpan.TicksPerMinute;
        return TimeSpan.FromTicks(unit - now.Ticks % unit);
    }
}
