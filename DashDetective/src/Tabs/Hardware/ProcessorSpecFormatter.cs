using System;
using System.Globalization;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Formats the Processor card's spec rows. Clocks read to one decimal of GHz — the precision a static
/// spec sheet wants, deliberately unlike <c>CpuSpeedFormatter</c>, which keeps two for the live rail.
/// Always InvariantCulture, matching the app's convention; a missing figure yields the neutral "—".
///
/// <b>Each row takes the machine's own reading first and a catalog datasheet value only as a fallback.</b>
/// The live figure describes this chip as configured; the catalog describes the part as rated. They agree
/// on a healthy desktop, and where they cannot — a VM with no <c>cpufreq</c> and no cache topology, a WMI
/// field the firmware left empty — the datasheet is the honest answer for a part the machine has already
/// identified by name, which is the same standing the TDP and boost rows have always had.
/// </summary>
internal static class ProcessorSpecFormatter {
    /// <summary>The neutral placeholder, which the catalog tables also store where a fact does not exist
    /// for a part (an AMD card has no CUDA cores) — so it has to read as "nothing here", not as a value.</summary>
    private const string None = "—";

    /// <summary>Formats a clock speed in MHz as GHz to one decimal (3200 → "3.2 GHz").</summary>
    public static string Ghz(double mhz) =>
        (mhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " GHz";

    /// <summary>
    /// Composes the "Base / Boost" row. The base is the machine's own reading where it has one and the
    /// catalog's rated base otherwise; the boost has no machine source on either platform and is always the
    /// catalog's. When both sides are known the unit is shared ("4.7 / 5.3 GHz"); otherwise the known side
    /// carries its own unit and the missing side is "—".
    /// </summary>
    public static string BaseBoost(double baseMhz, string? boost, string? baseSpec = null) {
        var live = baseMhz > 0 ? Ghz(baseMhz) : "";
        var rated = IsBlank(baseSpec) ? "" : baseSpec!;
        var baseClock = live.Length > 0 ? live : rated;

        var hasBase = baseClock.Length > 0;
        var hasBoost = !IsBlank(boost);
        if (!hasBase && !hasBoost)
            return None;
        if (hasBase && hasBoost)
            return $"{WithoutGhz(baseClock)} / {boost}";

        return hasBase ? $"{baseClock} / {None}" : $"{None} / {boost}";
    }

    /// <summary>Formats the L3 cache size, which both platforms report in KB, as whole MB — falling back to
    /// the catalog's rated size when the machine describes no cache topology, which is the ordinary case in
    /// a VM.</summary>
    public static string CacheL3(long kilobytes, string? spec = null) {
        if (kilobytes > 0)
            return $"{kilobytes / 1024} MB";

        return IsBlank(spec) ? None : spec!;
    }

    /// <summary>A catalog field, or the neutral placeholder when the catalog had no entry for the part or
    /// no value for the field. The shape every row's fallback ends in.</summary>
    public static string Spec(string? value) => IsBlank(value) ? None : value!;

    /// <summary>Drops a trailing " GHz" so a catalog base clock can share the boost side's unit exactly as
    /// a numeric one does. Pure; unit-tested.</summary>
    internal static string WithoutGhz(string clock) {
        const string unit = " GHz";
        return clock.EndsWith(unit, StringComparison.Ordinal) ? clock[..^unit.Length] : clock;
    }

    /// <summary>True when a catalog field carries no usable value — absent, empty, or the placeholder the
    /// tables store where a fact does not exist for a part.</summary>
    private static bool IsBlank(string? value) =>
        string.IsNullOrEmpty(value) || string.Equals(value, None, StringComparison.Ordinal);
}
