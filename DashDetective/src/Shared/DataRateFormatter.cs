using System;
using System.Globalization;

namespace DashDetective.Shared;

/// <summary>
/// The unit a data rate is expressed in. Ordered small → large so a rate can be promoted by magnitude.
/// </summary>
public enum RateUnit {
    Kbps,
    Mbps,
    Gbps,
}

/// <summary>
/// Shared formatting for network data rates. Picks a display unit by magnitude — kbps below 1 Mbps,
/// Mbps up to 1 Gbps, Gbps above — using a decimal (1000) base to match Task Manager and adapter
/// link-speed conventions. Centralises what used to be a per-tab <c>FormatMbps</c> plus the adapter's
/// ad-hoc Gbps/Mbps switch, so every throughput readout, axis caption and link speed reads the same.
///
/// Callers that display several related values on a shared axis (download + upload) should pick ONE
/// unit from the shared peak via <see cref="UnitFor"/> and render each value with
/// <see cref="Convert"/> + <see cref="FormatValue"/>, so the whole panel reads in a single unit.
/// </summary>
public static class DataRateFormatter {
    /// <summary>The unit that best fits a rate given in Mbps.</summary>
    public static RateUnit UnitFor(double mbps) {
        if (mbps < 0)
            mbps = 0;
        if (mbps >= 1000)
            return RateUnit.Gbps;
        return mbps >= 1 ? RateUnit.Mbps : RateUnit.Kbps;
    }

    /// <summary>Converts a rate given in Mbps into the numeric value for <paramref name="unit"/>.</summary>
    public static double Convert(double mbps, RateUnit unit) {
        if (mbps < 0)
            mbps = 0;
        return unit switch {
            RateUnit.Gbps => mbps / 1000.0,
            RateUnit.Kbps => mbps * 1000.0,
            _ => mbps,
        };
    }

    /// <summary>The short label for a unit ("kbps", "Mbps", "Gbps").</summary>
    public static string Label(RateUnit unit) => unit switch {
        RateUnit.Gbps => "Gbps",
        RateUnit.Kbps => "kbps",
        _ => "Mbps",
    };

    /// <summary>Formats an already-converted value for a readout: whole number at ≥ 10, one decimal
    /// below (e.g. "93", "2.7"), so the display stays compact without losing precision on small rates.</summary>
    public static string FormatValue(double value) {
        if (value < 0)
            value = 0;
        return value >= 10
            ? Math.Round(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>Convenience: a full "value unit" string for a single rate given in Mbps (auto-scaled),
    /// e.g. "2.7 Mbps".</summary>
    public static string Format(double mbps) {
        var unit = UnitFor(mbps);
        return $"{FormatValue(Convert(mbps, unit))} {Label(unit)}";
    }

    /// <summary>Auto-scales a single rate (in Mbps) and returns its formatted value and unit label
    /// separately, for UIs that style the number and unit independently.</summary>
    public static (string Value, string Unit) Split(double mbps) {
        var unit = UnitFor(mbps);
        return (FormatValue(Convert(mbps, unit)), Label(unit));
    }
}
