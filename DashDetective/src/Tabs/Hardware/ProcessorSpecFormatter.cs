using System;
using System.Globalization;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Formats the Processor card's spec rows. Clocks read to one decimal of GHz — the precision a static
/// spec sheet wants, deliberately unlike <c>CpuSpeedFormatter</c>, which keeps two for the live rail.
/// Always InvariantCulture, matching the app's convention; a missing figure yields the neutral "—".
/// Each row prefers the machine's own reading; the datasheet is a fallback for a VM with no cpufreq or cache topology.
/// </summary>
internal static class ProcessorSpecFormatter {
    /// <summary>The placeholder; catalog tables also store it for a fact a part lacks, so it must read as absent.</summary>
    private const string None = "—";

    /// <summary>Formats a clock speed in MHz as GHz to one decimal (3200 → "3.2 GHz").</summary>
    public static string Ghz(double mhz) =>
        (mhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " GHz";

    /// <summary>Composes "Base / Boost", preferring the machine's base over the catalog's; when both are known the unit is shared ("4.7 / 5.3 GHz").</summary>
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

    /// <summary>Formats the L3 size (KB) as whole MB, falling back to the catalog when the machine reports no cache topology, as in a VM.</summary>
    public static string CacheL3(long kilobytes, string? spec = null) {
        if (kilobytes > 0)
            return $"{kilobytes / 1024} MB";

        return IsBlank(spec) ? None : spec!;
    }

    /// <summary>A catalog field, or the placeholder when the part or field is missing.</summary>
    public static string Spec(string? value) => IsBlank(value) ? None : value!;

    /// <summary>The machine's own socket designation, else the catalog's rated one.</summary>
    public static string Socket(string? live, string? spec) =>
        string.IsNullOrWhiteSpace(live) ? Spec(spec) : live;

    /// <summary>Drops a trailing " GHz" so a catalog base clock can share the boost side's unit.</summary>
    internal static string WithoutGhz(string clock) {
        const string unit = " GHz";
        return clock.EndsWith(unit, StringComparison.Ordinal) ? clock[..^unit.Length] : clock;
    }

    /// <summary>True when a catalog field is absent, empty, or the placeholder the tables store for a fact a part lacks.</summary>
    private static bool IsBlank(string? value) =>
        string.IsNullOrEmpty(value) || string.Equals(value, None, StringComparison.Ordinal);
}
