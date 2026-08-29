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
    /// <summary>Formats the time of day as "16:05:09" or "4:05:09 PM".</summary>
    public static string Format(DateTime time, ClockFormat format) =>
        time.ToString(format == ClockFormat.TwelveHour ? "h:mm:ss tt" : "HH:mm:ss",
                      CultureInfo.InvariantCulture);
}
