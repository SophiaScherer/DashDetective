using System.Globalization;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Formats the Processor card's spec rows. Clocks read to one decimal of GHz — the precision a static
/// spec sheet wants, deliberately unlike <c>CpuSpeedFormatter</c>, which keeps two for the live rail.
/// Always InvariantCulture, matching the app's convention; a missing figure yields the neutral "—".
/// </summary>
internal static class ProcessorSpecFormatter {
    /// <summary>Formats a clock speed in MHz as GHz to one decimal (3200 → "3.2 GHz").</summary>
    public static string Ghz(double mhz) =>
        (mhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " GHz";

    /// <summary>Composes the "Base / Boost" row from the WMI base clock and the catalog boost string.
    /// When both are known the unit is shared ("4.7 / 5.3 GHz"); otherwise the known side carries its own
    /// unit and the missing side is "—".</summary>
    public static string BaseBoost(double baseMhz, string? boost) {
        var hasBase = baseMhz > 0;
        var hasBoost = !string.IsNullOrEmpty(boost);
        if (!hasBase && !hasBoost) return "—";
        if (hasBase && hasBoost)
            return $"{(baseMhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)} / {boost}";
        return hasBase ? $"{Ghz(baseMhz)} / —" : $"— / {boost}";
    }

    /// <summary>Formats the L3 cache size, which WMI reports in KB, as whole MB ("—" when absent).</summary>
    public static string CacheL3(long kilobytes) =>
        kilobytes > 0 ? $"{kilobytes / 1024} MB" : "—";
}
