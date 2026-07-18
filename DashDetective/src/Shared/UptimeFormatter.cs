using System;

namespace DashDetective.Shared;

/// <summary>
/// Shared formatting for system uptime durations. Centralises what was a per-tab helper so every
/// "up time" readout reads the same.
/// </summary>
public static class UptimeFormatter {
    /// <summary>
    /// Formats a duration as "Nd Nh Nm", dropping any leading zero units
    /// (e.g. "3d 14h 22m", "5h 2m", "12m").
    /// </summary>
    public static string Format(TimeSpan uptime) {
        var days = (int)uptime.TotalDays;
        if (days > 0)
            return $"{days}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.Hours > 0)
            return $"{uptime.Hours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m";
    }
}
